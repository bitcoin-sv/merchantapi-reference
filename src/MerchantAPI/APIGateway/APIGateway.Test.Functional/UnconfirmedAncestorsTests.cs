// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MerchantAPI.APIGateway.Domain;
using MerchantAPI.APIGateway.Domain.Actions;
using MerchantAPI.APIGateway.Domain.Models.Events;
using MerchantAPI.APIGateway.Domain.ViewModels;
using MerchantAPI.APIGateway.Rest.ViewModels;
using MerchantAPI.APIGateway.Test.Functional.Mock;
using MerchantAPI.APIGateway.Test.Functional.Server;
using MerchantAPI.Common.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NBitcoin;


namespace MerchantAPI.APIGateway.Test.Functional
{
  [TestCategory("TestCategoryNo2")]
  [TestClass]
  public class UnconfirmedAncestorsTests : ZMQTestBase
  {
    MapiMock mapiMock;

    [TestInitialize]
    public override void TestInitialize()
    {
      base.TestInitialize();
      mapiMock = server.Services.GetRequiredService<IMapi>() as MapiMock;
    }

    [TestCleanup]
    public override void TestCleanup()
    {
      base.TestCleanup();
    }

    public override TestServer CreateServer(bool mockedServices, TestServer serverCallback, string dbConnectionString, IEnumerable<KeyValuePair<string, string>> overridenSettings = null)
    {
      // special - we need mAPI with different modes
      return new TestServerBase(DbConnectionStringDDL).CreateServer<MapiServer, APIGatewayTestsMockStartup, APIGatewayTestsStartupMapiMock>(mockedServices, serverCallback, dbConnectionString, overridenSettings);
    }

    private async Task<(string, string, int)> CreateUnconfirmedAncestorChainWithFaultAsync(
      string txHex1, string txId1, int length, int sendToMAPIRate, bool sendLastToMAPI = false, bool triggerFault = false, CancellationToken? cancellationToken = null)
    {
      if (!triggerFault)
      {
        return await CreateUnconfirmedAncestorChainAsync(txHex1, txId1, length, sendToMAPIRate, sendLastToMAPI, cancellationToken);
      }
      // we want to test difference between sendrawtxs and getmempoolancestors
      // if there is an error when saving unconfirmedancestors the first time,
      // getmempoolancestors is called on the next submit
      mapiMock.SimulateDbFault(Faults.FaultType.DbBeforeSavingUncommittedState, Faults.DbFaultComponent.MapiUnconfirmedAncestors);
      await CreateUnconfirmedAncestorChainAsync(txHex1, txId1, length, sendToMAPIRate, sendLastToMAPI, cancellationToken, System.Net.HttpStatusCode.InternalServerError);

      mapiMock.ClearMode();

      return await CreateUnconfirmedAncestorChainAsync(txHex1, txId1, length, sendToMAPIRate, sendLastToMAPI, cancellationToken, expectAlreadyInMempool: true);
    }

    [DataRow(false)]
    [DataRow(true)]
    [TestMethod]
    public async Task StoreUnconfirmedParentsOnSubmitTxAsync(bool triggerFault)
    {
      using CancellationTokenSource cts = new(cancellationTimeout);

      await RegisterNodesWithServiceAndWaitAsync(cts.Token);
      Assert.AreEqual(1, zmqService.GetActiveSubscriptions().Count());

      // Subscribe invalidtx events
      var invalidTxDetectedSubscription = EventBus.Subscribe<InvalidTxDetectedEvent>();

      // Create and submit first transaction
      var coin = availableCoins.Dequeue();
      var (txHex1, txId1) = CreateNewTransaction(coin, new Money(1000L));
      var response = await node0.RpcClient.SendRawTransactionAsync(HelperTools.HexStringToByteArray(txHex1), true, false, cts.Token);

      // Create chain based on first transaction with last transaction being submited to mAPI
      var (lastTxHex, lastTxId, mapiCount) = await CreateUnconfirmedAncestorChainWithFaultAsync(txHex1, txId1, 100, 0, true, triggerFault, cts.Token);

      // Check that first tx is in database
      var tx1 = await TxRepositoryPostgres.GetTransactionAsync((new uint256(txId1)).ToBytes());
      Assert.IsNotNull(tx1);
      Assert.AreNotEqual(0, tx1.TxInternalId);
      Assert.IsTrue(tx1.UnconfirmedAncestor);
      Assert.AreEqual(DateTime.MinValue, tx1.SubmittedAt);
      // Check last tx
      var lastTx = await TxRepositoryPostgres.GetTransactionAsync((new uint256(lastTxId)).ToBytes());
      Assert.IsNotNull(lastTx);
      Assert.AreNotEqual(0, lastTx.TxInternalId);
      Assert.IsFalse(lastTx.UnconfirmedAncestor);
      Assert.AreNotEqual(DateTime.MinValue, lastTx.SubmittedAt);

      // Validate that all of the inputs are already in the database
      await ValidateTxInputsAsync(txHex1, txId1, presentOnDB: !triggerFault);
      await ValidateTxInputsAsync(lastTxHex, lastTxId);
    }

    [DataRow(false)]
    [DataRow(true)]
    [TestMethod]
    public async Task AncestorsAreAlreadyInDBForSecondMAPITxAsync(bool triggerFault)
    {
      using CancellationTokenSource cts = new(cancellationTimeout);

      await RegisterNodesWithServiceAndWaitAsync(cts.Token);
      Assert.AreEqual(1, zmqService.GetActiveSubscriptions().Count());

      // Subscribe invalidtx events
      var invalidTxDetectedSubscription = EventBus.Subscribe<InvalidTxDetectedEvent>();

      // Create and submit first transaction
      var coin = availableCoins.Dequeue();
      var (txHex1, txId1) = CreateNewTransaction(coin, new Money(1000L));
      var response = await node0.RpcClient.SendRawTransactionAsync(HelperTools.HexStringToByteArray(txHex1), true, false, cts.Token);

      // Create chain based on first transaction with last transaction being submited to mAPI
      var (lastTxHex, lastTxId, mapiCount) = await CreateUnconfirmedAncestorChainWithFaultAsync(txHex1, txId1, 50, 0, true, triggerFault, cts.Token);

      // Validate that all of the inputs are already in the database
      await ValidateTxInputsAsync(lastTxHex, lastTxId);
    }

    [DataRow(false)]
    [DataRow(true)]
    [TestMethod]
    public async Task AllAncestorsAreNotInDBForSecondMAPITxIfChainContainsOtherTxsAsync(bool triggerFault)
    {
      using CancellationTokenSource cts = new(cancellationTimeout);

      await RegisterNodesWithServiceAndWaitAsync(cts.Token);
      Assert.AreEqual(1, zmqService.GetActiveSubscriptions().Count());

      // Subscribe invalidtx events
      var invalidTxDetectedSubscription = EventBus.Subscribe<InvalidTxDetectedEvent>();

      // Create and submit first transaction
      var coin = availableCoins.Dequeue();
      var (txHex1, txId1) = CreateNewTransaction(coin, new Money(1000L));
      var response = await node0.RpcClient.SendRawTransactionAsync(HelperTools.HexStringToByteArray(txHex1), true, false, cts.Token);

      // Create chain based on first transaction with last transaction being submited to mAPI
      var (lastTxHex, lastTxId, mapiCount) = await CreateUnconfirmedAncestorChainWithFaultAsync(txHex1, txId1, 50, 0, true, triggerFault, cts.Token);

      // Create another transaction through RPC 
      (lastTxHex, lastTxId, mapiCount) = await CreateUnconfirmedAncestorChainWithFaultAsync(lastTxHex, lastTxId, 1, 0, false, false, cts.Token);

      // Validate that inputs are not already in the database
      await ValidateTxInputsAsync(lastTxHex, lastTxId, false);
    }

    [DataRow(1, false)]
    [DataRow(1, true)]
    [DataRow(100, false)]
    [DataRow(100, true)]
    [TestMethod]
    public async Task CatchMempoolDSForUnconfirmedParentAsync(int chainLength, bool triggerFault)
    {
      using CancellationTokenSource cts = new(cancellationTimeout);

      await RegisterNodesWithServiceAndWaitAsync(cts.Token);
      Assert.AreEqual(1, zmqService.GetActiveSubscriptions().Count());

      // Subscribe invalidtx events
      var invalidTxDetectedSubscription = EventBus.Subscribe<InvalidTxDetectedEvent>();

      // Create and submit first transaction
      var coin = availableCoins.Dequeue();
      var (txHex1, txId1) = CreateNewTransaction(coin, new Money(1000L));
      var response = await node0.RpcClient.SendRawTransactionAsync(HelperTools.HexStringToByteArray(txHex1), true, false, cts.Token);

      // Create chain based on first transaction with last transaction being submited to mAPI
      var (lastTxHex, lastTxId, mapiCount) = await CreateUnconfirmedAncestorChainWithFaultAsync(txHex1, txId1, chainLength, 0, true, triggerFault, cts.Token);

      // DS first transaction
      Transaction.TryParse(txHex1, Network.RegTest, out Transaction dsTx);
      var dsTxCoin = new Coin(dsTx, 0);
      var (txHexDs, txIdDs) = CreateNewTransaction(dsTxCoin, new Money(500L));
      // Send transaction using RPC
      try
      {
        _ = await node0.RpcClient.SendRawTransactionAsync(HelperTools.HexStringToByteArray(txHexDs), true, false, cts.Token);
      }
      catch (Exception rpcException)
      {
        // Double spend will throw txn-mempool-conflict exception
        Assert.IsTrue(rpcException.Message.StartsWith("258: txn-mempool-conflict"));
      }

      // InvalidTx event should be fired
      var invalidTxEvent = await invalidTxDetectedSubscription.ReadAsync(cts.Token);
      Assert.AreEqual(InvalidTxRejectionCodes.TxMempoolConflict, invalidTxEvent.Message.RejectionCode);

      await CheckCallbacksAsync(1, cts.Token);

      // Check if callback was received 
      var calls = Callback.Calls;
      var callback = HelperTools.JSONDeserialize<JSONEnvelopeViewModel>(calls[0].request)
        .ExtractPayload<CallbackNotificationDoubleSpendViewModel>();

      Assert.AreEqual(CallbackReason.DoubleSpendAttempt, callback.CallbackReason);
      Assert.AreEqual(-1, callback.BlockHeight);
    }

    [DataRow(false)]
    [DataRow(true)]
    [TestMethod]
    public async Task NotifyMempoolDSForAllTxWithDsCheckInChainAsync(bool triggerFault)
    {
      using CancellationTokenSource cts = new(cancellationTimeout);

      await RegisterNodesWithServiceAndWaitAsync(cts.Token);
      Assert.AreEqual(1, zmqService.GetActiveSubscriptions().Count());

      // Subscribe invalidtx events
      var invalidTxDetectedSubscription = EventBus.Subscribe<InvalidTxDetectedEvent>();

      // Create and submit first transaction
      var coin = availableCoins.Dequeue();
      var (txHex1, txId1) = CreateNewTransaction(coin, new Money(1000L));
      var response = await node0.RpcClient.SendRawTransactionAsync(HelperTools.HexStringToByteArray(txHex1), true, false, cts.Token);

      // Create chain based on first transaction with every 10th transaction being submited to mAPI
      var (lastTxHex, lastTxId, mapiCount) = await CreateUnconfirmedAncestorChainWithFaultAsync(txHex1, txId1, 100, 10, false, triggerFault, cts.Token);

      // Create ds transaction
      Transaction.TryParse(txHex1, Network.RegTest, out Transaction dsTx);
      var dsTxCoin = new Coin(dsTx, 0);
      var (txHexDs, txIdDs) = CreateNewTransaction(dsTxCoin, new Money(500L));
      // Send transaction using RPC
      try
      {
        _ = await node0.RpcClient.SendRawTransactionAsync(HelperTools.HexStringToByteArray(txHexDs), true, false, cts.Token);
      }
      catch (Exception rpcException)
      {
        // Double spend will throw txn-mempool-conflict exception
        Assert.IsTrue(rpcException.Message.StartsWith("258: txn-mempool-conflict"));
      }

      // InvalidTx event should be fired
      var invalidTxEvent = await invalidTxDetectedSubscription.ReadAsync(cts.Token);
      Assert.AreEqual(InvalidTxRejectionCodes.TxMempoolConflict, invalidTxEvent.Message.RejectionCode);

      await CheckCallbacksAsync(mapiCount, cts.Token);

      var calls = Callback.Calls;
      var callback = HelperTools.JSONDeserialize<JSONEnvelopeViewModel>(calls[0].request)
        .ExtractPayload<CallbackNotificationDoubleSpendViewModel>();

      Assert.AreEqual(CallbackReason.DoubleSpendAttempt, callback.CallbackReason);
      Assert.AreEqual(-1, callback.BlockHeight);
    }

    [DataRow(false)]
    [DataRow(true)]
    [TestMethod]
    public async Task CatchDSOfBlockAncestorTxByBlockTxAsync(bool triggerFault)
    {
      using CancellationTokenSource cts = new(cancellationTimeout);

      await RegisterNodesWithServiceAndWaitAsync(cts.Token);
      Assert.AreEqual(1, zmqService.GetActiveSubscriptions().Count());

      // Create two transactions from same input
      var coin = availableCoins.Dequeue();
      var (txHex1, txId1) = CreateNewTransaction(coin, new Money(1000L));
      var (txHexDS, txIdDS) = CreateNewTransaction(coin, new Money(500L));

      // Subscribe invalidtx events
      var invalidTxDetectedSubscription = EventBus.Subscribe<InvalidTxDetectedEvent>();

      // Submit transactions
      var response = await node0.RpcClient.SendRawTransactionAsync(HelperTools.HexStringToByteArray(txHex1), true, false, cts.Token);

      // Create chain based on first transaction
      var (lastTxHex, lastTxId, mapiCount) = await CreateUnconfirmedAncestorChainWithFaultAsync(txHex1, txId1, 100, 0, true, triggerFault, cts.Token);

      var parentBlockHash = await rpcClient0.GetBestBlockHashAsync();
      var parentBlockHeight = (await rpcClient0.GetBlockHeaderAsync(parentBlockHash)).Height;

      // Mine a new block containing mAPI transaction and its whole unconfirmed ancestor chain 
      var b1Hash = (await rpcClient0.GenerateAsync(1)).Single();

      await CheckCallbacksAsync(1, cts.Token);

      var calls = Callback.Calls;
      var signedJSON = HelperTools.JSONDeserialize<SignedPayloadViewModel>(calls[0].request);
      var notification = HelperTools.JSONDeserialize<CallbackNotificationViewModelBase>(signedJSON.Payload);
      Assert.AreEqual(CallbackReason.MerkleProof, notification.CallbackReason);
      
      // Mine sibling block to b1 - without any additional transaction
      var (b2, _) = await MineNextBlockAsync(Array.Empty<Transaction>(), false, parentBlockHash);

      // Mine a child block to b2, containing txDS. This will create a longer chain and we should be notified about doubleSpend
      var txDS = HelperTools.ParseBytesToTransaction(HelperTools.HexStringToByteArray(txHexDS));
      var (b3, _) = await MineNextBlockAsync(new[] { txDS }, true, b2, parentBlockHeight + 2);

      // Check if b3 was accepted
      var currentBestBlock = await rpcClient0.GetBestBlockHashAsync();
      Assert.AreEqual(b3.GetHash().ToString(), currentBestBlock, "b3 was not activated");

      await CheckCallbacksAsync(2, cts.Token);

      calls = Callback.Calls;
      signedJSON = HelperTools.JSONDeserialize<SignedPayloadViewModel>(calls[1].request);
      var dsNotification = HelperTools.JSONDeserialize<CallbackNotificationDoubleSpendViewModel>(signedJSON.Payload);
      Assert.AreEqual(CallbackReason.DoubleSpend, dsNotification.CallbackReason);
      Assert.AreEqual(txIdDS, dsNotification.CallbackPayload.DoubleSpendTxId);
    }

    [DataRow(false)]
    [DataRow(true)]
    [TestMethod]
    public async Task CatchDSOfMempoolAncestorTxByBlockTxAsync(bool triggerFault)
    {
      using CancellationTokenSource cts = new(cancellationTimeout);

      await RegisterNodesWithServiceAndWaitAsync(cts.Token);
      Assert.AreEqual(1, zmqService.GetActiveSubscriptions().Count());

      // Create two transactions from same input
      var coin = availableCoins.Dequeue();
      var (txHex1, txId1) = CreateNewTransaction(coin, new Money(1000L));
      var (txHex2, txId2) = CreateNewTransaction(coin, new Money(500L));


      var tx2 = HelperTools.ParseBytesToTransaction(HelperTools.HexStringToByteArray(txHex2));
      // Transactions should not be the same
      Assert.AreNotEqual(txHex1, txHex2);

      // Submit transaction
      var response = await node0.RpcClient.SendRawTransactionAsync(HelperTools.HexStringToByteArray(txHex1), true, false, cts.Token);

      // Create chain based on first transaction with last transaction being sent to mAPI
      var (lastTxHex, lastTxId, mapiCount) = await CreateUnconfirmedAncestorChainWithFaultAsync(txHex1, txId1, 100, 0, true, triggerFault, cts.Token);

      var mempoolTxs = await rpcClient0.GetRawMempool();

      // Transactions should be in mempool 
      Assert.IsTrue(mempoolTxs.Contains(txId1), "Submitted tx1 not found in mempool");

      Assert.AreEqual(0, Callback.Calls.Length);

      // Mine a new block containing tx2
      await MineNextBlockAsync(new[] { tx2 });

      var mempoolTxs2 = await rpcClient0.GetRawMempool();

      // Tx should no longer be in mempool
      Assert.IsFalse(mempoolTxs2.Contains(txId1), "Submitted tx1 should not be found in mempool");

      await CheckCallbacksAsync(1, cts.Token);

      var calls = Callback.Calls;
      var callback = HelperTools.JSONDeserialize<JSONEnvelopeViewModel>(calls[0].request)
        .ExtractPayload<CallbackNotificationDoubleSpendViewModel>();

      Assert.AreEqual(CallbackReason.DoubleSpend, callback.CallbackReason);
      Assert.AreEqual(new uint256(lastTxId), new uint256(callback.CallbackTxId));
      Assert.AreEqual(new uint256(txId2), new uint256(callback.CallbackPayload.DoubleSpendTxId));

    }
  }
}
