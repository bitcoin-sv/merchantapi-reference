// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using MerchantAPI.APIGateway.Domain;
using MerchantAPI.APIGateway.Domain.Actions;
using MerchantAPI.APIGateway.Domain.Models;
using MerchantAPI.APIGateway.Domain.Models.Events;
using MerchantAPI.APIGateway.Domain.ViewModels;
using MerchantAPI.APIGateway.Rest.Services;
using MerchantAPI.APIGateway.Rest.ViewModels;
using MerchantAPI.APIGateway.Test.Functional.Server;
using MerchantAPI.Common.EventBus;
using MerchantAPI.Common.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NBitcoin;
using NBitcoin.Altcoins;


namespace MerchantAPI.APIGateway.Test.Functional
{
  [TestClass]
  public class UnconfirmedAncestorsTestss : TestBaseWithBitcoind
  {
    private int cancellationTimeout = 30000; // 30 seconds
    public ZMQSubscriptionService zmqService;

    [TestInitialize]
    public override void TestInitialize()
    {
      base.TestInitialize();
      zmqService = server.Services.GetRequiredService<ZMQSubscriptionService>();
      ApiKeyAuthentication = AppSettings.RestAdminAPIKey;
      InsertFeeQuote();

      // Wait until all events are processed to avoid race conditions - we need to  finish subscribing to ZMQ before checking for any received notifications
      WaitUntilEventBusIsIdle();
    }

    [TestCleanup]
    public override void TestCleanup()
    {
      base.TestCleanup();
    }

    private async Task RegisterNodesWithServiceAndWaitAsync(CancellationToken cancellationToken)
    {
      var subscribedToZMQSubscription = eventBus.Subscribe<ZMQSubscribedEvent>();

      // Register nodes with service
      RegisterNodesWithService(cancellationToken);

      // Wait for subscription event so we can make sure that service is listening to node
      _ = await subscribedToZMQSubscription.ReadAsync(cancellationToken);

      // Unsubscribe from event bus
      eventBus.TryUnsubscribe(subscribedToZMQSubscription);
    }

    private void RegisterNodesWithService(CancellationToken cancellationToken)
    {
      // Register all nodes with service
      var nodes = this.NodeRepository.GetNodes();
      foreach (var node in nodes)
      {
        eventBus.Publish(new NodeAddedEvent() { CreationDate = DateTime.UtcNow, CreatedNode = node });
      }
    }

    private async Task<(string, string, int)> CreateUnconfirmedAncestorChainAsync(string txHex1, string txId1, int length, int sendToMAPIRate, bool sendLastToMAPI = false, CancellationToken? cancellationToken = null)
    {
      var curTxHex = txHex1;
      var curTxId = txId1;
      var mapiTxCount = 0;
      for (int i = 0; i < length; i++)
      {
        Transaction.TryParse(curTxHex, Network.RegTest, out Transaction curTx);
        var curTxCoin = new Coin(curTx, 0);
        (curTxHex, curTxId) = CreateNewTransaction(curTxCoin, new Money(1000L));

        // Submit every sendToMAPIRate tx to mapi with dsCheck
        if ((sendToMAPIRate != 0 && i % sendToMAPIRate == 0) || (sendLastToMAPI && i == length - 1))
        {
          var payload = await SubmitTransactionAsync(curTxHex, true, true);
          Assert.AreEqual(payload.ReturnResult, "success");
          mapiTxCount++;
        }
        else
        {
          _ = await node0.RpcClient.SendRawTransactionAsync(HelperTools.HexStringToByteArray(curTxHex), true, false, cancellationToken);
        }
      }

      return (curTxHex, curTxId, mapiTxCount);
    }
   
    [TestMethod]
    public async Task StoreUnconfirmedParentsOnSubmitTx()
    {
      using CancellationTokenSource cts = new CancellationTokenSource(cancellationTimeout);

      await RegisterNodesWithServiceAndWaitAsync(cts.Token);
      Assert.AreEqual(1, zmqService.GetActiveSubscriptions().Count());

      // Subscribe invalidtx events
      var invalidTxDetectedSubscription = eventBus.Subscribe<InvalidTxDetectedEvent>();

      // Create and submit first transaction
      var coin = availableCoins.Dequeue();
      var (txHex1, txId1) = CreateNewTransaction(coin, new Money(1000L));
      var response = await node0.RpcClient.SendRawTransactionAsync(HelperTools.HexStringToByteArray(txHex1), true, false, cts.Token);

      // Create chain based on first transaction with last transaction being submited to mAPI
      var (lastTxHex, lastTxId, mapiCount) = await CreateUnconfirmedAncestorChainAsync(txHex1, txId1, 100, 0, true, cts.Token);

      // Check that first tx is in database
      long? txInternalId1 = await TxRepositoryPostgres.GetTransactionInternalId((new uint256(txId1)).ToBytes());
      Assert.IsTrue(txInternalId1.HasValue);
      Assert.AreNotEqual(0, txInternalId1.Value);
    }
   
    [TestMethod]
    public async Task CatchMempoolDSForUnconfirmedParent()
    {
      using CancellationTokenSource cts = new CancellationTokenSource(cancellationTimeout);

      await RegisterNodesWithServiceAndWaitAsync(cts.Token);
      Assert.AreEqual(1, zmqService.GetActiveSubscriptions().Count());

      // Subscribe invalidtx events
      var invalidTxDetectedSubscription = eventBus.Subscribe<InvalidTxDetectedEvent>();

      // Create and submit first transaction
      var coin = availableCoins.Dequeue();
      var (txHex1, txId1) = CreateNewTransaction(coin, new Money(1000L));
      var response = await node0.RpcClient.SendRawTransactionAsync(HelperTools.HexStringToByteArray(txHex1), true, false, cts.Token);

      // Create chain based on first transaction with last transaction being submited to mAPI
      var (lastTxHex, lastTxId, mapiCount) = await CreateUnconfirmedAncestorChainAsync(txHex1, txId1, 100, 0, true, cts.Token);

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
        Assert.AreEqual("258: txn-mempool-conflict", rpcException.Message);
      }

      // InvalidTx event should be fired
      var invalidTxEvent = await invalidTxDetectedSubscription.ReadAsync(cts.Token);
      Assert.AreEqual(InvalidTxRejectionCodes.TxMempoolConflict, invalidTxEvent.Message.RejectionCode);
      
      WaitUntilEventBusIsIdle();

      // Check if callback was received 
      var calls = Callback.Calls;
      Assert.AreEqual(1, calls.Length);
      var callback = HelperTools.JSONDeserialize<JSONEnvelopeViewModel>(calls[0].request)
        .ExtractPayload<CallbackNotificationDoubleSpendViewModel>();

      Assert.AreEqual(CallbackReason.DoubleSpendAttempt, callback.CallbackReason);
    }

    [TestMethod]
    public async Task NotifyMempoolDSForAllTxWithDsCheckInChain()
    {
      using CancellationTokenSource cts = new CancellationTokenSource(cancellationTimeout);

      await RegisterNodesWithServiceAndWaitAsync(cts.Token);
      Assert.AreEqual(1, zmqService.GetActiveSubscriptions().Count());

      // Subscribe invalidtx events
      var invalidTxDetectedSubscription = eventBus.Subscribe<InvalidTxDetectedEvent>();

      // Create and submit first transaction
      var coin = availableCoins.Dequeue();
      var (txHex1, txId1) = CreateNewTransaction(coin, new Money(1000L));
      var response = await node0.RpcClient.SendRawTransactionAsync(HelperTools.HexStringToByteArray(txHex1), true, false, cts.Token);

      // Create chain based on first transaction with every 10th transaction being submited to mAPI
      var (lastTxHex, lastTxId, mapiCount) = await CreateUnconfirmedAncestorChainAsync(txHex1, txId1, 100, 10, false, cts.Token);

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
        Assert.AreEqual("258: txn-mempool-conflict", rpcException.Message);
      }

      // InvalidTx event should be fired
      var invalidTxEvent = await invalidTxDetectedSubscription.ReadAsync(cts.Token);
      Assert.AreEqual(InvalidTxRejectionCodes.TxMempoolConflict, invalidTxEvent.Message.RejectionCode);

      WaitUntilEventBusIsIdle();

      // Check if correct number of callbacks was received
      var calls = Callback.Calls;
      Assert.AreEqual(mapiCount, calls.Length);
      var callback = HelperTools.JSONDeserialize<JSONEnvelopeViewModel>(calls[0].request)
        .ExtractPayload<CallbackNotificationDoubleSpendViewModel>();

      Assert.AreEqual(CallbackReason.DoubleSpendAttempt, callback.CallbackReason);
    }

    [TestMethod]
    public async Task CatchDSOfBlockAncestorTxByBlockTx()
    {
      using CancellationTokenSource cts = new CancellationTokenSource(cancellationTimeout);

      await RegisterNodesWithServiceAndWaitAsync(cts.Token);
      Assert.AreEqual(1, zmqService.GetActiveSubscriptions().Count());

      // Create two transactions from same input
      var coin = availableCoins.Dequeue();
      var (txHex1, txId1) = CreateNewTransaction(coin, new Money(1000L));
      var (txHexDS, txIdDS) = CreateNewTransaction(coin, new Money(500L));

      // Subscribe invalidtx events
      var invalidTxDetectedSubscription = eventBus.Subscribe<InvalidTxDetectedEvent>();

      // Submit transactions
      var response = await node0.RpcClient.SendRawTransactionAsync(HelperTools.HexStringToByteArray(txHex1), true, false, cts.Token);

      // Create chain based on first transaction
      var (lastTxHex, lastTxId, mapiCount) = await CreateUnconfirmedAncestorChainAsync(txHex1, txId1, 100, 0, true, cts.Token);

      var parentBlockHash = await rpcClient0.GetBestBlockHashAsync();
      var parentBlockHeight = (await rpcClient0.GetBlockHeaderAsync(parentBlockHash)).Height;

      // Mine a new block containing mAPI transaction and its whole unconfirmed ancestor chain 
      var b1Hash = (await rpcClient0.GenerateAsync(1)).Single();

      WaitUntilEventBusIsIdle();

      var calls = Callback.Calls;
      Assert.AreEqual(1, calls.Length);
      var signedJSON = HelperTools.JSONDeserialize<SignedPayloadViewModel>(calls[0].request);
      var notification = HelperTools.JSONDeserialize<CallbackNotificationViewModelBase>(signedJSON.Payload);
      Assert.AreEqual(CallbackReason.MerkleProof, notification.CallbackReason);
      
      // Mine sibling block to b1 - without any additional transaction
      var (b2, _) = await MineNextBlockAsync(new Transaction[0], false, parentBlockHash);

      // Mine a child block to b2, containing txDS. This will create a longer chain and we should be notified about doubleSpend
      var txDS = HelperTools.ParseBytesToTransaction(HelperTools.HexStringToByteArray(txHexDS));
      var (b3, _) = await MineNextBlockAsync(new[] { txDS }, true, b2, parentBlockHeight + 2);

      // Check if b3 was accepted
      var currentBestBlock = await rpcClient0.GetBestBlockHashAsync();
      Assert.AreEqual(b3.GetHash().ToString(), currentBestBlock, "b3 was not activated");
      WaitUntilEventBusIsIdle();

      calls = Callback.Calls;
      Assert.AreEqual(2, calls.Length);
      signedJSON = HelperTools.JSONDeserialize<SignedPayloadViewModel>(calls[1].request);
      var dsNotification = HelperTools.JSONDeserialize<CallbackNotificationDoubleSpendViewModel>(signedJSON.Payload);
      Assert.AreEqual(CallbackReason.DoubleSpend, dsNotification.CallbackReason);
      Assert.AreEqual(txIdDS, dsNotification.CallbackPayload.DoubleSpendTxId);
    }

    [TestMethod]
    public async Task CatchDSOfMempoolAncestorTxByBlockTx()
    {
      using CancellationTokenSource cts = new CancellationTokenSource(cancellationTimeout);

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
      var (lastTxHex, lastTxId, mapiCount) = await CreateUnconfirmedAncestorChainAsync(txHex1, txId1, 100, 0, true, cts.Token);

      var mempoolTxs = await rpcClient0.GetRawMempool();

      // Transactions should be in mempool 
      Assert.IsTrue(mempoolTxs.Contains(txId1), "Submitted tx1 not found in mempool");

      Assert.AreEqual(0, Callback.Calls.Length);

      // Mine a new block containing tx2
      await MineNextBlockAsync(new[] { tx2 });

      var mempoolTxs2 = await rpcClient0.GetRawMempool();

      // Tx should no longer be in mempool
      Assert.IsFalse(mempoolTxs2.Contains(txId1), "Submitted tx1 should not be found in mempool");
      WaitUntilEventBusIsIdle();

      var calls = Callback.Calls;
      Assert.AreEqual(1, calls.Length);

      var callback = HelperTools.JSONDeserialize<JSONEnvelopeViewModel>(calls[0].request)
        .ExtractPayload<CallbackNotificationDoubleSpendViewModel>();

      Assert.AreEqual(CallbackReason.DoubleSpend, callback.CallbackReason);
      Assert.AreEqual(new uint256(lastTxId), new uint256(callback.CallbackTxId));
      Assert.AreEqual(new uint256(txId2), new uint256(callback.CallbackPayload.DoubleSpendTxId));

    }
  }
}
