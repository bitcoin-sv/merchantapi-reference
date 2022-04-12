// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain;
using MerchantAPI.APIGateway.Domain.Actions;
using MerchantAPI.APIGateway.Domain.Models;
using MerchantAPI.APIGateway.Domain.Models.Events;
using MerchantAPI.APIGateway.Test.Functional.Attributes;
using MerchantAPI.APIGateway.Test.Functional.Mock;
using MerchantAPI.APIGateway.Test.Functional.Server;
using MerchantAPI.Common.Json;
using MerchantAPI.Common.Test.Clock;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Test.Functional
{
  [TestCategory("TestCategoryNo4")]
  [TestClass]
  public class MempoolCheckerTestWithBitcoind : ZMQTestBase
  {
    MapiMock mapiMock;
    IMempoolChecker mempoolChecker;

    [TestInitialize]
    public override void TestInitialize()
    {
      base.TestInitialize();
      mapiMock = server.Services.GetRequiredService<IMapi>() as MapiMock;
      mempoolChecker = server.Services.GetRequiredService<IMempoolChecker>();

      PublishBlockHashToEventBus(rpcClient0.GetBestBlockHashAsync().Result);
      WaitUntilEventBusIsIdle();
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

    [TestMethod]
    public async Task GetRawMempoolTwoNodes()
    {
      // startup another node and link it to the first node
      var node1 = StartBitcoind(1, new BitcoindProcess[] { node0 });

      using CancellationTokenSource cts = new(cancellationTimeout);

      await RegisterNodesWithServiceAndWaitAsync(cts.Token);

      var mempoolTxs = await RpcMultiClient.GetRawMempool();
      Assert.AreEqual(0, mempoolTxs.Length);

      var (txHex1, txId1) = CreateNewTransaction(availableCoins.Dequeue(), new Money(1000L));
      var (txHex2, txId2) = CreateNewTransaction(availableCoins.Dequeue(), new Money(100L));

      var payload = await SubmitTransactionAsync(txHex1, true, true);
      Assert.AreEqual("success", payload.ReturnResult);

      await WaitForTxToBeAcceptedToMempool(node1, txId1, cts.Token);
      _ = await node1.RpcClient.SendRawTransactionAsync(HelperTools.HexStringToByteArray(txHex2), true, false, cts.Token);

      await WaitForTxToBeAcceptedToMempool(node0, txId2, cts.Token);

      // Both transactions should be in mempool
      mempoolTxs = await node0.RpcClient.GetRawMempool();
      Assert.AreEqual(2, mempoolTxs.Length);
      Assert.IsTrue(mempoolTxs.Contains(txId1));
      Assert.IsTrue(mempoolTxs.Contains(txId2));

      Assert.AreEqual(0, Callback.Calls.Length);

      StopBitcoind(node1);
    }

    [TestMethod]
    public async Task CheckMempool_AfterSubmitSingleTx()
    {
      // create transaction and submit it to the node
      var (txHex, txHash) = CreateNewTransaction();
      mapiMock.SimulateMode(Faults.SimulateSendTxsResponse.NodeFailsAfterSendRawTxs);

      var payloadSubmit = await SubmitTransactionAsync(txHex);
      Assert.AreEqual("success", payloadSubmit.ReturnResult);
      await AssertTxStatus(txHash, TxStatus.Mempool);
      var mempoolTxs = await rpcClient0.GetRawMempool();
      Assert.AreEqual(0, mempoolTxs.Length);
      var tx = (await TxRepositoryPostgres.GetMissingTransactionsAsync(mempoolTxs)).Single();

      // check mempool fails because of mode
      mapiMock.SimulateMode(Faults.SimulateSendTxsResponse.NodeFailsWhenSendRawTxs, Faults.FaultType.SimulateSendTxsMempoolChecker);
      bool success = await mempoolChecker.CheckMempoolAndResubmitTxs(0);
      Assert.IsFalse(success);
      mempoolTxs = await rpcClient0.GetRawMempool();
      Assert.AreEqual(0, mempoolTxs.Length);
      var txResubmitted = (await TxRepositoryPostgres.GetMissingTransactionsAsync(Array.Empty<string>())).Single();
      Assert.AreEqual(tx.SubmittedAt, txResubmitted.SubmittedAt);

      // check mempool should succeed
      mapiMock.ClearMode();
      success = await mempoolChecker.CheckMempoolAndResubmitTxs(0);
      Assert.IsTrue(success);
      Assert.AreEqual(txHash, (await rpcClient0.GetRawMempool()).Single());
      txResubmitted = await TxRepositoryPostgres.GetTransactionAsync(new uint256(txHash).ToBytes());
      Assert.IsTrue(tx.SubmittedAt < txResubmitted.SubmittedAt);
    }

    [TestMethod]
    public async Task CheckMempool_TxKnown()
    {
      var txList = new List<Tx>();

      var (txHex, txId) = CreateNewTransaction();
      txList.Add(CreateNewTx(txId, txHex, false, null, false));

      await TxRepositoryPostgres.InsertOrUpdateTxsAsync(txList, false);

      // send tx directly to the node
      (byte[] transaction, bool allowhighfees, bool dontCheckFees, bool listUnconfirmedAncestors, Dictionary<string, object> config)[] transactionsToSubmit =
      {
        (txList.First().TxPayload, true, false, false,  null)
      };

      var txResponses = await node0.RpcClient.SendRawTransactionsAsync(transactionsToSubmit, null);
      Assert.IsNull(txResponses.Invalid);
      Assert.IsNull(txResponses.Known);
      // on second submit known is filled
      txResponses = await node0.RpcClient.SendRawTransactionsAsync(transactionsToSubmit, null);
      Assert.IsNotNull(txResponses.Known);

      // submit tx through mAPI
      (txHex, txId) = CreateNewTransaction();
      txList.Add(CreateNewTx(txId, txHex, false, null, false));

      mapiMock.SimulateMode(Faults.SimulateSendTxsResponse.NodeFailsAfterSendRawTxs);
      var payloadSubmit = await SubmitTransactionAsync(txHex);
      Assert.AreEqual("success", payloadSubmit.ReturnResult);

      var txInDb = await TxRepositoryPostgres.GetTransactionAsync(new uint256(txId).ToBytes());
      // known txs are resubmitted successfully
      bool success = await mempoolChecker.CheckMempoolAndResubmitTxs(0);
      Assert.IsTrue(success);
      var txResubmitted = await TxRepositoryPostgres.GetTransactionAsync(new uint256(txId).ToBytes());
      Assert.IsTrue(txResubmitted.SubmittedAt > txInDb.SubmittedAt);
    }

    [TestMethod]
    public async Task ResubmitTxMempoolFull()
    {
      var (txHex, txId) = CreateNewTransaction();
      mapiMock.SimulateMode(Faults.SimulateSendTxsResponse.NodeReturnsMempoolFull);
      var payloadSubmit = await SubmitTransactionAsync(txHex);
      Assert.AreEqual("failure", payloadSubmit.ReturnResult);
      Assert.AreEqual(NodeRejectCode.MapiRetryMempoolError, payloadSubmit.ResultDescription);

      // not in mempool, because inserted directly into DB
      var txList = new List<Tx>
      {
        CreateNewTx(txId, txHex, false, null, false)
      };
      await TxRepositoryPostgres.InsertOrUpdateTxsAsync(txList, false);

      var parentBlockHash = await rpcClient0.GetBestBlockHashAsync();
      await CreateTransactionAndMineBlock(parentBlockHash);
      WaitUntilEventBusIsIdle();
      
      mapiMock.SimulateMode(Faults.SimulateSendTxsResponse.NodeReturnsMempoolFull, Faults.FaultType.SimulateSendTxsMempoolChecker);
      bool success = await mempoolChecker.CheckMempoolAndResubmitTxs(0);
      Assert.IsFalse(success);
      // resubmitted even if no block
      mapiMock.ClearMode();
      success = await mempoolChecker.CheckMempoolAndResubmitTxs(0);
      Assert.IsTrue(success);
    }

    [TestMethod]
    public async Task CheckMempool_TxWithSavedPolicy()
    {
      RestAuthentication = MockedIdentityBearerAuthentication;
      (var txHex, _) = CreateNewTransaction();
      string policies = "{\"maxscriptsizepolicy\":106}";
      string policiesTooLow = "{\"maxscriptsizepolicy\":-1}";

      InsertFeeQuote(MockedIdentity);
      SetPoliciesForCurrentFeeQuote(policies, MockedIdentity, emptyFeeQuoteRepo: true);

      mapiMock.SimulateMode(Faults.SimulateSendTxsResponse.NodeFailsAfterSendRawTxs);
      var payloadSubmit = await SubmitTransactionAsync(txHex);
      Assert.AreEqual("success", payloadSubmit.ReturnResult);

      mapiMock.ClearMode();

      using (MockedClock.NowIs(DateTime.UtcNow.AddMinutes(AppSettings.QuoteExpiryMinutes.Value)))
      {
        SetPoliciesForCurrentFeeQuote(policiesTooLow, MockedIdentity, emptyFeeQuoteRepo: false);

        (var txHex1, _) = CreateNewTransaction();
        var failedSubmit = await SubmitTransactionAsync(txHex1);
        Assert.AreEqual("failure", failedSubmit.ReturnResult);

        var mempoolTx = await rpcClient0.GetRawMempool();
        Assert.AreEqual(0, mempoolTx.Length);
        var tx = (await TxRepositoryPostgres.GetMissingTransactionsAsync(mempoolTx)).Single();
        Assert.AreEqual(policies, tx.Policies);
        // check mempool with saved policies should succeed 
        bool success = await mempoolChecker.CheckMempoolAndResubmitTxs(0);
        Assert.IsTrue(success);
        var txResubmitted = await TxRepositoryPostgres.GetTransactionAsync(tx.TxExternalId.ToBytes());
        Assert.IsTrue(tx.SubmittedAt < txResubmitted.SubmittedAt);
        Assert.AreEqual(policies, txResubmitted.Policies);
      }
    }

    [TestMethod]
    public async Task ResubmitTxLongList()
    {
      // make additional 100 coins
      foreach (var coin in GetCoins(base.rpcClient0, 100))
      {
          availableCoins.Enqueue(coin);
      }

      var txList = new List<Tx>();
      for (int i = 0; i < 110; i++)
      {
          var (txHex, txId) = CreateNewTransaction();
          txList.Add(CreateNewTx(txId, txHex, false, null, true));
      };
      await TxRepositoryPostgres.InsertOrUpdateTxsAsync(txList, false);

      var txSubmitted = await TxRepositoryPostgres.GetTransactionAsync(new uint256(txList.First().TxExternalId).ToBytes());

      var mempoolTxs = await rpcClient0.GetRawMempool();
      Assert.AreEqual(0, mempoolTxs.Length);
      Assert.AreEqual(110, (await TxRepositoryPostgres.GetMissingTransactionsAsync(mempoolTxs)).Length);

      bool success = await mempoolChecker.CheckMempoolAndResubmitTxs(0);
      mempoolTxs = await rpcClient0.GetRawMempool();
      Assert.AreEqual(110, (await rpcClient0.GetRawMempool()).Length);
      Assert.AreEqual(0, (await TxRepositoryPostgres.GetMissingTransactionsAsync(mempoolTxs)).Length);
      Assert.IsTrue(success);
      var txResubmitted = await TxRepositoryPostgres.GetTransactionAsync(new uint256(txList.First().TxExternalId).ToBytes());
      Assert.IsTrue(txSubmitted.SubmittedAt < txResubmitted.SubmittedAt);
    }

    [TestMethod]
    public async Task MineBlockWithSingleTx()
    {
      var mempoolTxs = await rpcClient0.GetRawMempool();
      Assert.AreEqual(0, mempoolTxs.Length);

      using CancellationTokenSource cts = new(cancellationTimeout);

      await RegisterNodesWithServiceAndWaitAsync(cts.Token);

      // Subscribe new block events
      var newBlockDiscoveredSubscription = EventBus.Subscribe<NewBlockDiscoveredEvent>();
      var newBlockAvailableInDbSubscription = EventBus.Subscribe<NewBlockAvailableInDB>();

      WaitUntilEventBusIsIdle();

      var (txHex1, txId1) = CreateNewTransaction(availableCoins.Dequeue(), new Money(1000L)); // submitted through mAPI
      var (txHex2, txId2) = CreateNewTransaction(availableCoins.Dequeue(), new Money(100L)); // mined in block

      var payload = await SubmitTransactionAsync(txHex1, false, false);
      Assert.AreEqual("success", payload.ReturnResult);

      var mempoolTx1 = await rpcClient0.GetRawMempool();
      Assert.AreEqual(mempoolTx1.Single(), txId1);

      var tx2 = HelperTools.ParseBytesToTransaction(HelperTools.HexStringToByteArray(txHex2));
      // Mine a new block containing tx2
      await MineNextBlockAsync(new[] { tx2 });
      // new block available must be processed
      WaitUntilEventBusIsIdle();

      mempoolTxs = await rpcClient0.GetRawMempool();

      // Tx1 should no longer be in mempool
      Assert.IsFalse(mempoolTxs.Contains(txId2));
      Assert.IsTrue(mempoolTxs.Contains(txId1));

      // Nothing to update since tx2 is already in mempool
      var txs = (await TxRepositoryPostgres.GetMissingTransactionsAsync(mempoolTxs));
      Assert.AreEqual(0, txs.Length);

      await mempoolChecker.CheckMempoolAndResubmitTxs(0);
      var tx2db = await TxRepositoryPostgres.GetTransactionAsync(new uint256(txId2).ToBytes());
      Assert.IsNull(tx2db);
    }

    private async Task<(NBitcoin.Block block, string txId)> CreateTransactionAndMineBlock(string parentBlockHash, bool throwOnError = true, bool dsCheck = false, Transaction transaction = null)
    {
      if (transaction == null)
      {
        transaction = CreateNewTransactionTx();
      }
      var (txHex, txId) = (transaction.ToHex(), transaction.GetHash().ToString());
      await SubmitTransactionAsync(txHex, dsCheck: dsCheck);
      var tx = HelperTools.ParseBytesToTransaction(HelperTools.HexStringToByteArray(txHex));
      var (forkBlock, _) = await MineNextBlockAsync(new[] { tx }, throwOnError: throwOnError, parentBlockHash: parentBlockHash);
      return (forkBlock, txId);
    }

    [TestMethod]
    public async Task DoubleReorgCheck()
    {
      var parentBlockHash = await rpcClient0.GetBestBlockHashAsync();
      // chain A
      var (forkBlock, txIdA) = await CreateTransactionAndMineBlock(parentBlockHash);

      // new longer chain B
      var (newBlock, txIdB1) = await CreateTransactionAndMineBlock(parentBlockHash, false);
      var( _, txIdB2) = await CreateTransactionAndMineBlock(newBlock.GetHash().ToString());

      WaitUntilEventBusIsIdle();

      Assert.IsFalse((await TxRepositoryPostgres.GetBlockAsync(forkBlock.GetHash().ToBytes())).OnActiveChain);
      var q = await QueryTransactionStatus(txIdA);
      await AssertQueryTxAsync(q, txIdA, "success", confirmations: null, txStatus: TxStatus.Blockchain);

      var mempoolTxs = await rpcClient0.GetRawMempool();
      Assert.AreEqual(1, mempoolTxs.Length);
      Assert.IsTrue(mempoolTxs.Contains(txIdA));
      await mempoolChecker.CheckMempoolAndResubmitTxs(0);
      // since txA si present in mempool, nothing is resubmitted
      var txToResubmit = await TxRepositoryPostgres.GetMissingTransactionsAsync(mempoolTxs);
      Assert.AreEqual(0, txToResubmit.Length);

      // increase chain A - now longer
      var (forkBlock2, _) = await CreateTransactionAndMineBlock(forkBlock.GetHash().ToString(), false);
      await CreateTransactionAndMineBlock(forkBlock2.GetHash().ToString());

      WaitUntilEventBusIsIdle();

      Assert.IsTrue((await TxRepositoryPostgres.GetBlockAsync(forkBlock.GetHash().ToBytes())).OnActiveChain);
      q = await QueryTransactionStatus(txIdB1);
      await AssertQueryTxAsync(q, txIdB1, "success", confirmations: null, txStatus: TxStatus.Blockchain);

      mempoolTxs = await rpcClient0.GetRawMempool();
      Assert.AreEqual(2, mempoolTxs.Length);
      Assert.IsTrue(mempoolTxs.Contains(txIdB1));
      Assert.IsTrue(mempoolTxs.Contains(txIdB2));
      // since txBs are present in mempool, nothing is resubmitted
      txToResubmit = await TxRepositoryPostgres.GetMissingTransactionsAsync(mempoolTxs);
      Assert.AreEqual(0, txToResubmit.Length);

      // if mempool would be empty, then txBs would be resubmitted
      txToResubmit = await TxRepositoryPostgres.GetMissingTransactionsAsync(Array.Empty<string>());
      Assert.AreEqual(2, txToResubmit.Length);
    }

    private (string txHex, string txId) CreateTransactionWithParentTransaction(string parentHex)
    {
      Transaction.TryParse(parentHex, Network.RegTest, out Transaction curTx);
      var curTxCoin = new Coin(curTx, 0);
      return CreateNewTransaction(curTxCoin, new Money(1000L));
    }

    [DataRow(1)]
    [DataRow(5)]
    [TestMethod]
    public async Task MissingInputsMaxRetriesReached(int nTxs)
    {
      // create chained transactions: tx0 -> tx1 ...
      var coin = availableCoins.Dequeue();
      var (tx0Hex, tx0Id) = CreateNewTransaction(coin, new Money(1000L));

      var txList = new List<Tx>();
      var parent = tx0Hex;
      while(nTxs > 0)
      {
        var (txHex, txId) = CreateTransactionWithParentTransaction(parent);
        txList.Add(CreateNewTx(txId, txHex, false, null, true));
        parent = txHex;
        nTxs--;
      }
      // add tx0 as last
      txList.Add(CreateNewTx(tx0Id, tx0Hex, false, null, true));

      var tx1Hex = HelperTools.ByteToHexString(txList.First().TxPayload);
      var tx1_payload = await SubmitTransactionAsync(tx1Hex);
      Assert.AreEqual("failure", tx1_payload.ReturnResult);
      Assert.AreEqual("Missing inputs", tx1_payload.ResultDescription);

      // for resubmit txs must be present in db
      await TxRepositoryPostgres.InsertOrUpdateTxsAsync(txList, false);
      try
      {
        using CancellationTokenSource cts = new(cancellationTimeout);
        var t =await node0.RpcClient.SendRawTransactionAsync(txList.First().TxPayload, true, false, cts.Token);
      }
      catch (Common.BitcoinRpc.RpcException ex)
      {
        Assert.AreEqual("Missing inputs", ex.Message);
      }
      // we resubmit ordered by id - child txs before tx0, batch = 1: only tx0 is resubmitted
      var (success, txsWithMissingInputs) = await mapiMock.ResubmitMissingTransactions(1);
      Assert.IsTrue(success);
      foreach (var tx in txList.SkipLast(1))
      {
        var txInternalId = await TxRepositoryPostgres.GetTransactionInternalIdAsync(new uint256(tx.TxExternalId).ToBytes());
        Assert.IsTrue(txsWithMissingInputs.Any(x => x == txInternalId));
      }

      // to submit all other txs, resubmitMissingTransactions must be called twice
      (success, txsWithMissingInputs) = await mapiMock.ResubmitMissingTransactions(1);
      Assert.IsTrue(success);
      Assert.AreEqual(0, txsWithMissingInputs.Count);
    }

    [TestMethod]
    [OverrideSetting("AppSettings:MempoolCheckerMissingInputsRetries", 2)]
    public async Task MissingInputsResubmitSuccessfully()
    {
      AppSettings.MempoolCheckerMissingInputsRetries = 2;

      await CheckMissingInputsMaxRetries();
    }

    [TestMethod]
    [OverrideSetting("AppSettings:MempoolCheckerMissingInputsRetries", 0)]
    public async Task MissingInputsNoRetries()
    {
      AppSettings.MempoolCheckerMissingInputsRetries = 0;
      await CheckMissingInputsMaxRetries(false);
    }

    private async Task CheckMissingInputsMaxRetries(bool resubmitted = true)
    {
      var parentBlockHash = await rpcClient0.GetBestBlockHashAsync();

      var (txIdA, _, newBlock) = await TwoTxsFromSameCoinOnDifferentChainsAB(parentBlockHash);

      int retries = AppSettings.MempoolCheckerMissingInputsRetries.Value;
      Tx txAInDb;
      while (retries > -1)
      {
        txAInDb = await TxRepositoryPostgres.GetTransactionAsync(new uint256(txIdA).ToBytes());
        Assert.AreEqual(TxStatus.Blockchain, txAInDb.TxStatus);
        (newBlock, _) = await CreateTransactionAndMineBlock(newBlock.GetHash().ToString());
        WaitUntilEventBusIsIdle();
        await mempoolChecker.CheckMempoolAndResubmitTxs(0);
        retries--;
      }
      txAInDb = await TxRepositoryPostgres.GetTransactionAsync(new uint256(txIdA).ToBytes());
      Assert.AreEqual(TxStatus.MissingInputsMaxRetriesReached, txAInDb.TxStatus);
      if (!resubmitted)
      {
        Assert.AreEqual(txAInDb.ReceivedAt.Ticks, txAInDb.SubmittedAt.Ticks, 10);
      }
      var (success, txsWithMissingInputs) = await mapiMock.ResubmitMissingTransactions();
      Assert.IsTrue(success);
      Assert.AreEqual(0, txsWithMissingInputs.Count);
    }

    private async Task<(string txIdA, string TxIdB, NBitcoin.Block lastBlock)> TwoTxsFromSameCoinOnDifferentChainsAB(string parentBlockHash, bool dsCheck = false)
    {
      // create two transactions with same input
      var coin = availableCoins.Dequeue();
      var txA = CreateNewTransactionTx(coin, new Money(1000L));
      var txB = CreateNewTransactionTx(coin, new Money(500L));
      // chain A
      var (_, txIdA) = await CreateTransactionAndMineBlock(parentBlockHash, dsCheck: dsCheck, transaction: txA);
      // new longer chain B
      var (newBlock, _) = await CreateTransactionAndMineBlock(parentBlockHash, false, dsCheck: dsCheck);
      var (lastBlock, txIdB) = await CreateTransactionAndMineBlock(newBlock.GetHash().ToString(), dsCheck: dsCheck, transaction: txB);

      WaitUntilEventBusIsIdle();

      var mempoolTxs = await rpcClient0.GetRawMempool();
      Assert.AreEqual(0, mempoolTxs.Length);

      var (success, txsWithMissingInputs) = await mapiMock.ResubmitMissingTransactions();
      Assert.IsTrue(success);
      var txInternalId = await TxRepositoryPostgres.GetTransactionInternalIdAsync(new uint256(txA.GetHash()).ToBytes());
      Assert.AreEqual(txInternalId, txsWithMissingInputs.Single());
      return (txIdA, txIdB, lastBlock);
    }

    [TestMethod]
    public async Task MissingInputsCheckNotifications()
    {
      using CancellationTokenSource cts = new(cancellationTimeout);

      await RegisterNodesWithServiceAndWaitAsync(cts.Token);

      // Subscribe removedFromMempool events
      var removedFromMempoolSubscription = EventBus.Subscribe<RemovedFromMempoolEvent>();
      var parentBlockHash = await rpcClient0.GetBestBlockHashAsync();

      var (txIdA, txIdB, _) = await TwoTxsFromSameCoinOnDifferentChainsAB(parentBlockHash, dsCheck: true);

      // new chain C1 -> C2 -> C3
      var (newBlock, _) = await CreateTransactionAndMineBlock(parentBlockHash, false, dsCheck: true);
      (newBlock, _) = await CreateTransactionAndMineBlock(newBlock.GetHash().ToString(), false, dsCheck: true);
      (newBlock, _) = await CreateTransactionAndMineBlock(newBlock.GetHash().ToString(), dsCheck: true);

      WaitUntilEventBusIsIdle();

      var mempoolTxs = await rpcClient0.GetRawMempool();
      Assert.AreEqual(2, mempoolTxs.Length);
      Assert.IsFalse(mempoolTxs.Contains(txIdA));

      var collisionInBlockEvent = await removedFromMempoolSubscription.ReadAsync(cts.Token);
      Assert.AreEqual(txIdA, collisionInBlockEvent.Message.TxId);

      var txAInDb = await TxRepositoryPostgres.GetTransactionAsync(new uint256(txIdA).ToBytes());
      Assert.AreEqual(TxStatus.Blockchain, txAInDb.TxStatus);

      var notification = (await TxRepositoryPostgres.GetNotificationsForTestsAsync()).Single();
      Assert.AreEqual(CallbackReason.DoubleSpend, notification.NotificationType);
      var calls = Callback.Calls;
      Assert.AreEqual(1, calls.Length);

      var success = await mempoolChecker.CheckMempoolAndResubmitTxs(0);
      Assert.IsTrue(success);
      WaitUntilEventBusIsIdle();

      // new events should not be fired
      var notificationAfterResubmit = (await TxRepositoryPostgres.GetNotificationsForTestsAsync()).Single();
      Assert.AreEqual(notification.TxInternalId, notificationAfterResubmit.TxInternalId);
      calls = Callback.Calls;
      Assert.AreEqual(1, calls.Length);

      mempoolTxs = await rpcClient0.GetRawMempool();
      Assert.AreEqual(2, mempoolTxs.Length);
      Assert.IsFalse(mempoolTxs.Contains(txIdA));
    }
  }
}
