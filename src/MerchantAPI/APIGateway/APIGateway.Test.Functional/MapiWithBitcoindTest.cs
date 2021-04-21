// Copyright (c) 2020 Bitcoin Association

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using MerchantAPI.APIGateway.Domain;
using MerchantAPI.APIGateway.Domain.Models.Events;
using MerchantAPI.APIGateway.Domain.ViewModels;
using MerchantAPI.APIGateway.Rest.ViewModels;
using MerchantAPI.APIGateway.Test.Functional.Server;
using MerchantAPI.Common.Json;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NBitcoin;
using NBitcoin.Altcoins;

namespace MerchantAPI.APIGateway.Test.Functional
{
  [TestClass]
  public class MapiWithBitcoindTest : TestBaseWithBitcoind
  {
    private int cancellationTimeout = 30000; // 30 seconds

    [TestInitialize]
    public override void TestInitialize()
    {
      base.TestInitialize();
      InsertFeeQuote();
    }


    [TestCleanup]
    public override void TestCleanup()
    {
      base.TestCleanup();
    }


    (string txHex, string txId) CreateNewTransaction()
    {
      // Create transaction from a coin 
      var coin = availableCoins.Dequeue();
      var amount = new Money(1000L);
      return CreateNewTransaction(coin, amount);
    }


    [TestMethod]
    public async Task SubmitTransaction()
    {
      var (txHex, txId) = CreateNewTransaction();

      var payload = await SubmitTransactionAsync(txHex);

      Assert.AreEqual(payload.ReturnResult, "success");

      // Try to fetch tx from the node
      var txFromNode = await rpcClient0.GetRawTransactionAsBytesAsync(txId);

      Assert.AreEqual(txHex, HelperTools.ByteToHexString(txFromNode));
    }



    [TestMethod]
    public async Task SubmitTransactionAndWaitForProof()
    {
      var (txHex, txId) = CreateNewTransaction();

      var payload = await SubmitTransactionAsync(txHex, merkleProof: true);

      Assert.AreEqual(payload.ReturnResult, "success");

      // Try to fetch tx from the node
      var txFromNode = await rpcClient0.GetRawTransactionAsBytesAsync(txId);
      Assert.AreEqual(txHex, HelperTools.ByteToHexString(txFromNode));

      Assert.AreEqual(0, Callback.Calls.Length);

      var notificationEventSubscription = eventBus.Subscribe<NewNotificationEvent>();
      // This is not absolutely necessary, since we ar waiting for NotificationEvent too, but it helps
      // with troubleshooting:
      var generatedBlock = await GenerateBlockAndWaitForItTobeInsertedInDBAsync();
      loggerTest.LogInformation($"Generated block {generatedBlock} should contain our transaction");

      await WaitForEventBusEventAsync(notificationEventSubscription,
        $"Waiting for merkle notification event for tx {txId}",
        (evt) => evt.NotificationType == CallbackReason.MerkleProof
                 && new uint256(evt.TransactionId) == new uint256(txId)
      );
      WaitUntilEventBusIsIdle();

      // Check if callback was received
      Assert.AreEqual(1, Callback.Calls.Length);

      var callback = HelperTools.JSONDeserialize<JSONEnvelopeViewModel>(Callback.Calls[0].request)
        .ExtractPayload<CallbackNotificationMerkeProofViewModel>();
      Assert.AreEqual(CallbackReason.MerkleProof, callback.CallbackReason);
      Assert.AreEqual(new uint256(txId), new uint256(callback.CallbackTxId));
      Assert.AreEqual(new uint256(txId), new uint256(callback.CallbackPayload.TxOrId));
      Assert.IsTrue(callback.CallbackPayload.Target.NumTx >0, "A block header contained in merkle proof should have at least 1 tx. This indicates a problem in serialization code.");

    }

    [Ignore("Test ignored untill SVN-1361 will be merged into develop")]
    [TestMethod]
    public async Task SubmitTransactionAndWaitForProof2()
    {
      var (txHex, txId) = CreateNewTransaction();

      var payload = await SubmitTransactionAsync(txHex, merkleProof: true, merkleFormat: MerkleFormat.TSC);

      Assert.AreEqual(payload.ReturnResult, "success");

      // Try to fetch tx from the node
      var txFromNode = await rpcClient0.GetRawTransactionAsBytesAsync(txId);
      Assert.AreEqual(txHex, HelperTools.ByteToHexString(txFromNode));

      Assert.AreEqual(0, Callback.Calls.Length);

      var notificationEventSubscription = eventBus.Subscribe<NewNotificationEvent>();
      // This is not absolutely necessary, since we ar waiting for NotificationEvent too, but it helps
      // with troubleshooting:
      var generatedBlock = await GenerateBlockAndWaitForItTobeInsertedInDBAsync();
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

    [Ignore("Test ignored untill SVN-1361 will be merged into develop")]
    [TestMethod]
    public async Task SubmitTransactionWithInvalidMerkleFormat()
    {
      var (txHex, txId) = CreateNewTransaction();

      var payload = await SubmitTransactionAsync(txHex, merkleProof: true, merkleFormat: "WRONG") ;

      Assert.AreEqual("failure", payload.ReturnResult);

    }

    [TestMethod]
    public async Task SubmitTransactionWithMissingInputs()
    {
      // Just use some transaction  from mainnet - it will not exists on our regtest
      var reqContent = new StringContent($"{{ \"rawtx\": \"{tx2Hex}\" }}");
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);
      var response =
        await Post<SignedPayloadViewModel>(MapiServer.ApiMapiSubmitTransaction, client, reqContent, HttpStatusCode.OK);

      var payload = response.response.ExtractPayload<SubmitTransactionResponseViewModel>();

      Assert.AreEqual("failure", payload.ReturnResult);
      Assert.AreEqual("Missing inputs", payload.ResultDescription);
    }


    async Task<QueryTransactionStatusResponseViewModel> QueryTransactionStatus(string txId)
    {
      var response = await Get<SignedPayloadViewModel>(
        client, MapiServer.ApiMapiQueryTransactionStatus + txId, HttpStatusCode.OK);

      return response.ExtractPayload<QueryTransactionStatusResponseViewModel>();
    }

    [TestMethod]
    public async Task QueryTransactionStatusNonExistent()
    {
      var payload = await QueryTransactionStatus(txC1Hash);

      Assert.AreEqual("failure", payload.ReturnResult); 
    }

    [TestMethod]
    public async Task SubmitAndQueryTransactionStatus()
    {

      var (txHex, txHash) = CreateNewTransaction();


      var payloadSubmit = await SubmitTransactionAsync(txHex);

      Assert.AreEqual("success", payloadSubmit.ReturnResult);


      var q1 = await QueryTransactionStatus(txHash);

      Assert.AreEqual(txHash, q1.Txid);
      Assert.AreEqual("success", q1.ReturnResult);
      Assert.AreEqual(null, q1.Confirmations);

      _ = await rpcClient0.GenerateAsync(1);

      var q2 = await QueryTransactionStatus(txHash);

      Assert.AreEqual("success", q2.ReturnResult);
      Assert.AreEqual(1, q2.Confirmations);

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
      using CancellationTokenSource cts = new CancellationTokenSource(cancellationTimeout);

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
    public async Task SubmitTxsWithOneThatCausesDS()
    {
      using CancellationTokenSource cts = new CancellationTokenSource(cancellationTimeout);

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
      var payload = await SubmitTransactionsAsync(new string[]{ txHex2, txHex3});
      
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

    
  }
}
