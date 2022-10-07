// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain;
using MerchantAPI.APIGateway.Domain.Actions;
using MerchantAPI.APIGateway.Domain.Models.Events;
using MerchantAPI.APIGateway.Domain.ViewModels;
using MerchantAPI.APIGateway.Rest.ViewModels;
using MerchantAPI.APIGateway.Test.Functional.Server;
using MerchantAPI.Common.BitcoinRpc;
using MerchantAPI.Common.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NBitcoin;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Test.Functional
{
  [TestCategory("TestCategoryNo3")]
  [TestClass]
  public class MapiWithBitcoindTest : MapiWithBitcoindTestBase
  {

    [TestInitialize]
    public override void TestInitialize()
    {
      base.TestInitialize();
    }


    [TestCleanup]
    public override void TestCleanup()
    {
      base.TestCleanup();
    }

    [TestMethod]
    public async Task GetFeeQuote()
    {
      // some fields are filled by calling node
      var response = await Get<SignedPayloadViewModel>(
        Client, MapiServer.ApiMapiQueryFeeQuote, HttpStatusCode.OK);
      var payload = response.ExtractPayload<FeeQuoteViewModelGet>();
      Assert.IsNotNull(payload);

      // generate block to make sure cached info version is not enough
      await node0.RpcClient.GenerateAsync(1);

      // test node unreachable
      StopBitcoind(node0);
      response = await Get<SignedPayloadViewModel>(
        Client, MapiServer.ApiMapiQueryFeeQuote, HttpStatusCode.ServiceUnavailable);
      Assert.IsNull(response);
    }

    [TestMethod]
    public async Task SubmitTransaction()
    {
      var (txHex, txId) = CreateNewTransaction();

      var payload = await SubmitTransactionAsync(txHex);

      Assert.AreEqual("success", payload.ReturnResult);

      // Try to fetch tx from the node
      var txFromNode = await rpcClient0.GetRawTransactionAsBytesAsync(txId);

      Assert.AreEqual(txHex, HelperTools.ByteToHexString(txFromNode));

      // generate block to make sure cached info version is not enough
      await node0.RpcClient.GenerateAsync(1);

      // test node unreachable
      var (txHex2, _) = CreateNewTransaction();
      StopBitcoind(node0);
      payload = await SubmitTransactionAsync(txHex2,
        expectedHttpStatusCode: HttpStatusCode.ServiceUnavailable, expectedHttpMessage: "Failed to connect to node(s).");
      Assert.IsNull(payload);
    }

    [TestMethod]
    public async Task SubmitTransactionWithNegativeOutput()
    {
      var tx = CreateNewTransactionTx();
      tx.Outputs.First().Value = -1L;
      var txHex = tx.ToHex();
      var txId = tx.GetHash().ToString();

      var payload = await SubmitTransactionAsync(txHex);

      Assert.AreEqual("failure", payload.ReturnResult);
      Assert.AreEqual("Negative inputs are not allowed", payload.ResultDescription);

      using CancellationTokenSource cts = new(cancellationTimeout);
      var tx_result = await Assert.ThrowsExceptionAsync<RpcException>(
        () => node0.RpcClient.SendRawTransactionAsync(tx.ToBytes(), true, false, cts.Token));
      Assert.AreEqual("16: bad-txns-vout-negative", tx_result.Message);
    }

    [TestMethod]
    public async Task SubmitSameTransactionMultipleTimesAsync()
    {
      using CancellationTokenSource cts = new(cancellationTimeout);

      var (txHex1, txId1) = CreateNewTransaction(); // mAPI, mAPI
      var (txHex2, txId2) = CreateNewTransaction(); // mAPI, RPC
      var (txHex3, txId3) = CreateNewTransaction(); // RPC, mAPI      

      var tx1_payload1 = await SubmitTransactionAsync(txHex1);
      var tx1_payload2 = await SubmitTransactionAsync(txHex1);

      Assert.AreEqual("success", tx1_payload1.ReturnResult);
      Assert.AreEqual("success", tx1_payload2.ReturnResult);
      Assert.AreEqual(NodeRejectCode.ResultAlreadyKnown, tx1_payload2.ResultDescription);

      var tx2_payload1 = await SubmitTransactionAsync(txHex2);
      Assert.AreEqual("success", tx2_payload1.ReturnResult);
      var tx2_result2 = await Assert.ThrowsExceptionAsync<RpcException>(
        () => node0.RpcClient.SendRawTransactionAsync(HelperTools.HexStringToByteArray(txHex2), true, false, cts.Token));
      Assert.AreEqual("Transaction already in the mempool", tx2_result2.Message);

      var tx3_result1 = await node0.RpcClient.SendRawTransactionAsync(HelperTools.HexStringToByteArray(txHex3), true, false, cts.Token);
      var tx3_payload2 = await SubmitTransactionAsync(txHex3);

      Assert.AreEqual(txId3, tx3_result1);
      Assert.AreEqual("success", tx3_payload2.ReturnResult);

      // Mine block and than resend all 3 transactions using mAPI
      var generatedBlock = await GenerateBlockAndWaitForItToBeInsertedInDBAsync();
      var tx1_payload3 = await SubmitTransactionAsync(txHex1);
      var tx2_payload3 = await SubmitTransactionAsync(txHex2);
      var tx3_payload3 = await SubmitTransactionAsync(txHex3);

      Assert.AreEqual("success", tx1_payload3.ReturnResult);
      Assert.AreEqual(NodeRejectCode.ResultAlreadyKnown, tx1_payload3.ResultDescription);
      Assert.AreEqual("success", tx2_payload3.ReturnResult);
      Assert.AreEqual(NodeRejectCode.ResultAlreadyKnown, tx2_payload3.ResultDescription);
      Assert.AreEqual("success", tx3_payload3.ReturnResult);
      Assert.AreEqual(NodeRejectCode.ResultAlreadyKnown, tx3_payload3.ResultDescription);
      Assert.IsNull(tx3_payload3.ConflictedWith);
    }

    [DataRow(false)]
    [DataRow(true)]
    [TestMethod]
    public async Task SubmitTransactionsWithSameInput(bool dsCheck)
    {
      var mapi = server.Services.GetRequiredService<IMapi>();
      var oldStatus = mapi.GetSubmitTxStatus();
      loggerTest.LogInformation("OldStatus:" + oldStatus.PrepareForLogging());

      var tx0 = CreateNewTransactionTx();

      // Create two transactions with same input
      var coin = availableCoins.Dequeue();
      var tx1 = CreateNewTransactionTx(coin, new Money(1000L));
      var tx2 = CreateNewTransactionTx(coin, new Money(500L));

      var txHexList = new string[] { tx0.ToHex(), tx1.ToHex(), tx2.ToHex() };
      var payload = await SubmitTransactionsAsync(txHexList, dsCheck);

      Assert.AreEqual(2, payload.Txs.Count(x => x.ReturnResult == "success"));

      // All txs are sent to node and one from tx1/tx2 fails:
      // which of them fails and why depends on threads execution
      var failedTx = payload.Txs.Single(x => x.ReturnResult == "failure");
      Assert.IsTrue(failedTx.ResultDescription == "18 txn-double-spend-detected" ||
                    failedTx.ResultDescription.StartsWith("258 txn-mempool-conflict"));
      // all three txs are sent to node, one rejected
      var status = mapi.GetSubmitTxStatus();
      Assert.AreEqual(oldStatus.Request + 1, status.Request);
      Assert.AreEqual(oldStatus.TxAuthenticatedUser, status.TxAuthenticatedUser);
      Assert.AreEqual(oldStatus.TxAnonymousUser + 3, status.TxAnonymousUser);
      Assert.AreEqual(oldStatus.Tx + 3, status.Tx);
      Assert.AreEqual(status.Tx / status.Request, status.AvgBatch);
      Assert.AreEqual(oldStatus.TxSentToNode + 3, status.TxSentToNode);
      Assert.AreEqual(oldStatus.TxAcceptedByNode + 2, status.TxAcceptedByNode);
      Assert.AreEqual(oldStatus.TxRejectedByNode + 1, status.TxRejectedByNode);
      Assert.AreEqual(oldStatus.TxSubmitException, status.TxSubmitException);
      Assert.AreEqual(oldStatus.TxResponseSuccess + 2, status.TxResponseSuccess);
      Assert.AreEqual(oldStatus.TxResponseFailure + 1, status.TxResponseFailure);
      Assert.AreEqual(oldStatus.TxResponseException, status.TxResponseException);
      loggerTest.LogInformation("Status:" + status.PrepareForLogging());
    }

    [TestMethod]
    public async Task SubmitTransactionAndWaitForProof()
    {
      using CancellationTokenSource cts = new(cancellationTimeout);

      var (txHex, txId) = CreateNewTransaction();

      var payload = await SubmitTransactionAsync(txHex, merkleProof: true);

      Assert.AreEqual("success", payload.ReturnResult);

      // Try to fetch tx from the node
      var txFromNode = await rpcClient0.GetRawTransactionAsBytesAsync(txId);
      Assert.AreEqual(txHex, HelperTools.ByteToHexString(txFromNode));

      Assert.AreEqual(0, Callback.Calls.Length);

      var notificationEventSubscription = EventBus.Subscribe<NewNotificationEvent>();
      // This is not absolutely necessary, since we ar waiting for NotificationEvent too, but it helps
      // with troubleshooting:
      var generatedBlock = await GenerateBlockAndWaitForItToBeInsertedInDBAsync();
      loggerTest.LogInformation($"Generated block {generatedBlock} should contain our transaction");

      await WaitForEventBusEventAsync(notificationEventSubscription,
        $"Waiting for merkle notification event for tx {txId}",
        (evt) => evt.NotificationType == CallbackReason.MerkleProof
                 && new uint256(evt.TransactionId) == new uint256(txId)
      );

      // Check if callback was received
      await CheckCallbacksAsync(1, cts.Token);

      var callback = HelperTools.JSONDeserialize<JSONEnvelopeViewModel>(Callback.Calls[0].request)
        .ExtractPayload<CallbackNotificationMerkeProofViewModel>();
      Assert.AreEqual(CallbackReason.MerkleProof, callback.CallbackReason);
      Assert.AreEqual(new uint256(txId), new uint256(callback.CallbackTxId));
      Assert.AreEqual(new uint256(txId), new uint256(callback.CallbackPayload.TxOrId));
      Assert.IsTrue(callback.CallbackPayload.Target.NumTx > 0, "A block header contained in merkle proof should have at least 1 tx. This indicates a problem in serialization code.");

    }

    [TestMethod]
    public async Task SubmitTransactionsAndWaitForProofs()
    {
      using CancellationTokenSource cts = new(cancellationTimeout);

      var tx1 = CreateNewTransactionTx(availableCoins.Dequeue(), new Money(1000L));
      var tx2 = CreateNewTransactionTx(availableCoins.Dequeue(), new Money(500L));
      var txHexList = new string[] { tx1.ToHex(), tx2.ToHex() };

      var payload = await SubmitTransactionsAsync(txHexList, merkleProof: true);

      Assert.AreEqual(0, payload.FailureCount);

      // Try to fetch tx from the node
      var txFromNode = await rpcClient0.GetRawTransactionAsBytesAsync(tx1.GetHash().ToString());
      Assert.AreEqual(tx1.ToHex(), HelperTools.ByteToHexString(txFromNode));
      txFromNode = await rpcClient0.GetRawTransactionAsBytesAsync(tx2.GetHash().ToString());
      Assert.AreEqual(tx2.ToHex(), HelperTools.ByteToHexString(txFromNode));

      Assert.AreEqual(0, Callback.Calls.Length);

      var notificationEventSubscription = EventBus.Subscribe<NewNotificationEvent>();
      // This is not absolutely necessary, since we ar waiting for NotificationEvent too, but it helps
      // with troubleshooting:
      var generatedBlock = await GenerateBlockAndWaitForItToBeInsertedInDBAsync();
      loggerTest.LogInformation($"Generated block {generatedBlock} should contain our transactions");

      WaitUntilEventBusIsIdle();

      // Check if callbacks were received
      await CheckCallbacksAsync(2, cts.Token);

      var callbacks = Callback.Calls.Select(x => 
      (HelperTools.JSONDeserialize<JSONEnvelopeViewModel>(x.request)).ExtractPayload<CallbackNotificationMerkeProofViewModel>());
      Assert.IsTrue(callbacks.All(x => x.CallbackReason == CallbackReason.MerkleProof));
      Assert.IsTrue(callbacks.Any(x => new uint256(x.CallbackTxId) == tx1.GetHash()));
      Assert.IsTrue(callbacks.Any(x => new uint256(x.CallbackTxId) == tx2.GetHash()));
      Assert.IsTrue(callbacks.All(x => x.CallbackTxId == x.CallbackPayload.TxOrId));
    }

    [TestMethod]
    public async Task SubmitTransactionAndWaitForProof2()
    {
      var (txHex, txId) = CreateNewTransaction();

      var payload = await SubmitTransactionAsync(txHex, merkleProof: true, merkleFormat: MerkleFormat.TSC);

      Assert.AreEqual("success", payload.ReturnResult);

      // Try to fetch tx from the node
      var txFromNode = await rpcClient0.GetRawTransactionAsBytesAsync(txId);
      Assert.AreEqual(txHex, HelperTools.ByteToHexString(txFromNode));

      Assert.AreEqual(0, Callback.Calls.Length);

      var notificationEventSubscription = EventBus.Subscribe<NewNotificationEvent>();
      // This is not absolutely necessary, since we ar waiting for NotificationEvent too, but it helps
      // with troubleshooting:
      var generatedBlock = await GenerateBlockAndWaitForItToBeInsertedInDBAsync();
      loggerTest.LogInformation($"Generated block {generatedBlock} should contain our transaction");

      await WaitForEventBusEventAsync(notificationEventSubscription,
        $"Waiting for merkle notification event for tx {txId}",
        (evt) => evt.NotificationType == CallbackReason.MerkleProof
                 && new uint256(evt.TransactionId) == new uint256(txId)
      );
      WaitUntilEventBusIsIdle();

      // Check if callback was received
      Assert.AreEqual(1, Callback.Calls.Length);

      // Verify that it parses merkleproof2
      var callback = HelperTools.JSONDeserialize<JSONEnvelopeViewModel>(Callback.Calls[0].request)
        .ExtractPayload<CallbackNotificationMerkeProof2ViewModel>();
      Assert.AreEqual(CallbackReason.MerkleProof, callback.CallbackReason);

      // Validate callback
      var blockHeader = BlockHeader.Parse(callback.CallbackPayload.Target, Network.RegTest);
      Assert.AreEqual(generatedBlock, blockHeader.GetHash());
      Assert.AreEqual(new uint256(txId), new uint256(callback.CallbackTxId));
      Assert.AreEqual(new uint256(txId), new uint256(callback.CallbackPayload.TxOrId));

    }

    [TestMethod]
    public async Task SubmitTransactionWithInvalidMerkleFormat()
    {
      var (txHex, _) = CreateNewTransaction();

      var payload = await SubmitTransactionAsync(txHex, merkleProof: true, merkleFormat: "WRONG");

      Assert.AreEqual("failure", payload.ReturnResult);

    }

    [TestMethod]
    public async Task SubmitTransactionWithMissingInputs()
    {
      // Just use some transaction  from mainnet - it will not exists on our regtest
      var reqContent = new StringContent($"{{ \"rawtx\": \"{tx2Hex}\" }}");
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);
      var response =
        await Post<SignedPayloadViewModel>(MapiServer.ApiMapiSubmitTransaction, Client, reqContent, HttpStatusCode.OK);

      var payload = response.response.ExtractPayload<SubmitTransactionResponseViewModel>();

      Assert.AreEqual("failure", payload.ReturnResult);
      Assert.AreEqual("Missing inputs", payload.ResultDescription);
    }


    [TestMethod]
    public async Task QueryTransactionStatusNonExistent()
    {
      var payload = await QueryTransactionStatus(txC1Hash);

      await AssertQueryTxAsync(payload, txC1Hash, "failure",
        "No such mempool transaction. Use -txindex to enable blockchain transaction queries. Use gettransaction for wallet transactions.");
    }

    [TestMethod]
    public async Task SubmitAndQueryTransactionStatus()
    {

      var (txHex, txHash) = CreateNewTransaction();

      var payloadSubmit = await SubmitTransactionAsync(txHex);

      Assert.AreEqual("success", payloadSubmit.ReturnResult);

      var q1 = await QueryTransactionStatus(txHash);

      await AssertQueryTxAsync(q1, txHash, "success", confirmations: null);

      // try to get with merkleProof
      q1 = await QueryTransactionStatus(txHash, true);
      // returns no error, only merkleProof is not present
      await AssertQueryTxAsync(q1, txHash, "success", confirmations: null);

      _ = await rpcClient0.GenerateAsync(1);

      // default query does not return merkleProof
      var q2 = await QueryTransactionStatus(txHash);

      await AssertQueryTxAsync(q2, txHash, "success", confirmations: 1);

      // check no merkle proof, but format set
      q2 = await QueryTransactionStatus(txHash, false, MerkleFormat.TSC);

      await AssertQueryTxAsync(q2, txHash, "success", confirmations: 1);

      // check defaultFormat value
      q2 = await QueryTransactionStatus(txHash, true);

      await AssertQueryTxAsync(q2, txHash, "success", confirmations: 1, checkMerkleProofWithMerkleFormat: MerkleFormat.TSC);

      // check merkle proof formats
      q2 = await QueryTransactionStatus(txHash, true, "");

      await AssertQueryTxAsync(q2, txHash, "success", confirmations: 1, checkMerkleProofWithMerkleFormat: "");

      q2 = await QueryTransactionStatus(txHash, true, MerkleFormat.TSC);

      await AssertQueryTxAsync(q2, txHash, "success", confirmations: 1, checkMerkleProofWithMerkleFormat: MerkleFormat.TSC);
      
      // test node unreachable
      StopBitcoind(node0);
      var payload = await QueryTransactionStatus(txHash, 
        expectedCode: HttpStatusCode.ServiceUnavailable, expectedHttpMessage: "Failed to connect to node(s).");
      Assert.IsNull(payload);
    }


    [TestMethod]
    public async Task SubmitToOneNodeButQueryTwo()
    {

      // Create transaction  and submit it to the first node
      var (txHex, txHash) = CreateNewTransaction();

      var payloadSubmit = await SubmitTransactionAsync(txHex);
      Assert.AreEqual("success", payloadSubmit.ReturnResult);

      // Check if transaction was received OK
      var q1 = await QueryTransactionStatus(txHash);
      Assert.AreEqual("success", q1.ReturnResult);


      // Create another node but do not connect it to the first one
      var node2 = CreateAndStartNode(2);

      // We should get mixed results, since only one node has the transaction
      var q2 = await QueryTransactionStatus(txHash);
      Assert.AreEqual("failure", q2.ReturnResult);
      Assert.AreEqual("Mixed results", q2.ResultDescription);

      // now stop the second node
      StopBitcoind(node2);

      // Query again.  The second node is unreachable, so it will be ignored
      var q3 = await QueryTransactionStatus(txHash);
      Assert.AreEqual("success", q3.ReturnResult);
    }

    [TestMethod]
    public async Task SubmitTxThatCausesDS()
    {
      using CancellationTokenSource cts = new(cancellationTimeout);

      // Create two transactions from same input
      var coin = availableCoins.Dequeue();
      var (txHex1, txId1) = CreateNewTransaction(coin, new Money(1000L));
      var (txHex2, txId2) = CreateNewTransaction(coin, new Money(500L));

      // Transactions should not be the same
      Assert.AreNotEqual(txHex1, txHex2);

      // Send first transaction 
      _ = await node0.RpcClient.SendRawTransactionAsync(HelperTools.HexStringToByteArray(txHex1), true, false, cts.Token);

      // Send second transaction using MAPI
      var payload = await SubmitTransactionAsync(txHex2);
      Assert.AreEqual("failure", payload.ReturnResult);
      Assert.AreEqual(1, payload.ConflictedWith.Length);
      Assert.AreEqual(txId1, payload.ConflictedWith.First().Txid);
    }

    [TestMethod]
    public async Task SubmitTxThatCollidesWithSameTxMultipleTimes()
    {
      using CancellationTokenSource cts = new(cancellationTimeout);

      // Create two transactions that use same 10 inputs
      int numOfOutputs = 10;
      Coin[] coins = new Coin[numOfOutputs];
      for (int i = 0; i < numOfOutputs; i++)
      {
        coins[i] = availableCoins.Dequeue();
      }
      var (txHex1, txId1) = CreateNewTransaction(coins, new Money(1000L));
      var (txHex2, txId2) = CreateNewTransaction(coins.Take(5).ToArray(), new Money(500L));
      var (txHex3, txId3) = CreateNewTransaction(coins.TakeLast(5).ToArray(), new Money(500L));

      // Send first transaction 
      _ = await node0.RpcClient.SendRawTransactionAsync(HelperTools.HexStringToByteArray(txHex1), true, false, cts.Token);

      // Send second and third transaction using MAPI
      var payload = await SubmitTransactionsAsync(new string[] { txHex2, txHex3 });

      // Both tx should have only one tx(txId1) in collided with result field
      foreach (var tx in payload.Txs)
      {
        Assert.AreEqual("failure", tx.ReturnResult);
        Assert.AreEqual(1, tx.ConflictedWith.Length);
        Assert.AreEqual(txId1, tx.ConflictedWith.First().Txid);
        Assert.IsFalse(string.IsNullOrEmpty(tx.ConflictedWith.First().Hex));
      }
    }


    [TestMethod]
    public async Task SubmitTxsWithOneThatCausesDS()
    {
      using CancellationTokenSource cts = new(cancellationTimeout);

      // Create two transactions from same input
      var coin = availableCoins.Dequeue();
      var (txHex1, txId1) = CreateNewTransaction(coin, new Money(1000L));
      var (txHex2, txId2) = CreateNewTransaction(coin, new Money(500L));
      var (txHex3, txId3) = CreateNewTransaction();

      // Transactions should not be the same
      Assert.AreNotEqual(txHex1, txHex2);
      Assert.AreNotEqual(txHex1, txHex3);
      Assert.AreNotEqual(txHex2, txHex3);

      // Send first transaction 
      _ = await node0.RpcClient.SendRawTransactionAsync(HelperTools.HexStringToByteArray(txHex1), true, false, cts.Token);

      // Send second and third transaction using MAPI
      var payload = await SubmitTransactionsAsync(new string[] { txHex2, txHex3 });

      // Should have one failure
      Assert.AreEqual(1, payload.FailureCount);

      // Check transactions
      Assert.AreEqual(2, payload.Txs.Length);
      var tx2 = payload.Txs.First(t => t.Txid == txId2);
      var tx3 = payload.Txs.First(t => t.Txid == txId3);

      // Tx2 should fail and Tx3 should succeed
      Assert.AreEqual("failure", tx2.ReturnResult);
      Assert.AreEqual("success", tx3.ReturnResult);

      // Tx2 should be conflicted transaction for Tx2
      Assert.AreEqual(1, tx2.ConflictedWith.Length);
      Assert.AreEqual(txId1, tx2.ConflictedWith.First().Txid);
    }

    [TestMethod]
    public async Task With2NodesOnlyOneDoubleSpendShouldBeSent()
    {
      using CancellationTokenSource cts = new(cancellationTimeout);

      // Create two transactions from same input
      var coin = availableCoins.Dequeue();
      var (txHex1, txId1) = CreateNewTransaction(coin, new Money(1000L));
      var (txHex2, txId2) = CreateNewTransaction(coin, new Money(500L));

      // Transactions should not be the same
      Assert.AreNotEqual(txHex1, txHex2);

      // Send first transaction using MAPI
      var payload = await SubmitTransactionAsync(txHex1, false, true);

      // start another node and connect the nodes
      // then wait for the new node to sync up before sending a DS tx
      var node1 = StartBitcoind(1, new BitcoindProcess[] { node0 });

      await SyncNodesBlocksAsync(cts.Token, node0, node1);

      Assert.AreEqual(1, await node1.RpcClient.GetConnectionCountAsync());

      await DisconnectNodeAndWait(node1, node0, 1, cts.Token);

      // Send second transaction 
      _ = await node1.RpcClient.SendRawTransactionAsync(HelperTools.HexStringToByteArray(txHex2), true, false, cts.Token);
      await node1.RpcClient.GenerateAsync(1);

      await AddNodeAndWait(node1, node0, 0, false, cts.Token);

      // We are sleeping here for a second to make sure that after the nodes were reconnected
      // there wasn't any additional notification sent because of node1
      await Task.Delay(1000);

      var notifications = await TxRepositoryPostgres.GetNotificationsForTestsAsync();
      foreach (var notification in notifications)
      {
        loggerTest.LogInformation($"NotificationType: {notification.NotificationType}; TxId: {notification.TxInternalId}");
      }
      Assert.AreEqual(1, notifications.Length);
      Assert.IsNotNull(notifications.Single().DoubleSpendTxId);
    }

    public async Task TestTwoNodesWithDifferentSettings()
    {
      using CancellationTokenSource cts = new(cancellationTimeout);

      var node1 = StartBitcoind(1, new BitcoindProcess[] { node0 }, argumentList: new() { "-maxscriptsizepolicy=100" });

      await SyncNodesBlocksAsync(cts.Token, node0, node1);

      // created scriptSize has around 105bytes
      (string txHex, string txHash) = CreateNewTransaction();
      var tx_result = await Assert.ThrowsExceptionAsync<RpcException>(
       () => node1.RpcClient.SendRawTransactionAsync(HelperTools.HexStringToByteArray(txHex), true, false, cts.Token));
       Assert.AreEqual("64: non-mandatory-script-verify-flag (Script is too big)", tx_result.Message);

      var payloadSubmit = await SubmitTransactionAsync(txHex);
      Assert.AreEqual("success", payloadSubmit.ReturnResult);
      // only node0 accepted the tx
      var mempoolTxs = await node0.RpcClient.GetRawMempool();
      Assert.AreEqual(1, mempoolTxs.Length);
      mempoolTxs = await node1.RpcClient.GetRawMempool();
      Assert.AreEqual(0, mempoolTxs.Length);
    }
  }
}
