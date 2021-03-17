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

    private async Task RegisterNodesWithServiceAndWait(CancellationToken cancellationToken)
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

    [TestMethod]
    public async Task StoreUnconfirmedParentsOnSubmitTx()
    {
      using CancellationTokenSource cts = new CancellationTokenSource(cancellationTimeout);

      await RegisterNodesWithServiceAndWait(cts.Token);
      Assert.AreEqual(1, zmqService.GetActiveSubscriptions().Count());

      // Subscribe invalidtx events
      var invalidTxDetectedSubscription = eventBus.Subscribe<InvalidTxDetectedEvent>();

      // Create and submit first transaction
      var coin = availableCoins.Dequeue();
      var (txHex1, txId1) = CreateNewTransaction(coin, new Money(1000L));
      var response = await node0.RpcClient.SendRawTransactionAsync(HelperTools.HexStringToByteArray(txHex1), true, false, cts.Token);

      // Create chain of 80 unconfirmed parents
      var curTxHex = txHex1;
      var curTxId = txId1;
      for (int i = 0; i < 80; i++)
      {
        Transaction.TryParse(curTxHex, Network.RegTest, out Transaction curTx);
        var curTxCoin = new Coin(curTx, 0);
        (curTxHex, curTxId) = CreateNewTransaction(curTxCoin, new Money(1000L));
        _ = await node0.RpcClient.SendRawTransactionAsync(HelperTools.HexStringToByteArray(curTxHex), true, false, cts.Token);
      }

      // Create second transaction using output from last tx in chain
      Transaction.TryParse(curTxHex, Network.RegTest, out Transaction lastTx);
      var lastTxCoin = new Coin(lastTx, 0);
      var (txHex2, txId2) = CreateNewTransaction(lastTxCoin, new Money(1000L));
      var payload2 = await SubmitTransactionAsync(txHex2, false, true);
      Assert.AreEqual(payload2.ReturnResult, "success");

      // Check that first tx is in database
      long? txInternalId1 = await TxRepositoryPostgres.GetTransactionInternalId((new uint256(txId1)).ToBytes());
      Assert.IsTrue(txInternalId1.HasValue);
      Assert.AreNotEqual(0, txInternalId1.Value);
    }

    [TestMethod]
    public async Task CatchMempoolDSForUnconfirmedParent()
    {
      using CancellationTokenSource cts = new CancellationTokenSource(cancellationTimeout);

      await RegisterNodesWithServiceAndWait(cts.Token);
      Assert.AreEqual(1, zmqService.GetActiveSubscriptions().Count());

      // Subscribe invalidtx events
      var invalidTxDetectedSubscription = eventBus.Subscribe<InvalidTxDetectedEvent>();

      // Create and submit first transaction
      var coin = availableCoins.Dequeue();
      var (txHex1, txId1) = CreateNewTransaction(coin, new Money(1000L));
      var response = await node0.RpcClient.SendRawTransactionAsync(HelperTools.HexStringToByteArray(txHex1), true, false, cts.Token);

      // Create chain based on first transaction
      var (lastTxHex, lastTxId) = await CreateUnconfirmedAncestorChain(txHex1, txId1, 100, cts.Token);

      // Create second transaction using output from last tx in chain
      Transaction.TryParse(lastTxHex, Network.RegTest, out Transaction lastTx);
      var lastTxCoin = new Coin(lastTx, 0);
      var (txHex2, txId2) = CreateNewTransaction(lastTxCoin, new Money(1000L));
      var payload2 = await SubmitTransactionAsync(txHex2, false, true);
      Assert.AreEqual(payload2.ReturnResult, "success");


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

      // Check if callback was received
      var calls = Callback.Calls;
      Assert.AreEqual(1, calls.Length);
      var callback = HelperTools.JSONDeserialize<JSONEnvelopeViewModel>(calls[0].request)
        .ExtractPayload<CallbackNotificationDoubleSpendViewModel>();

      Assert.AreEqual(CallbackReason.DoubleSpendAttempt, callback.CallbackReason);
    }


    [TestMethod]
    public async Task NotifyDSForAllTxWithDsCheckInChain()
    {
      using CancellationTokenSource cts = new CancellationTokenSource(cancellationTimeout);

      await RegisterNodesWithServiceAndWait(cts.Token);
      Assert.AreEqual(1, zmqService.GetActiveSubscriptions().Count());

      // Subscribe invalidtx events
      var invalidTxDetectedSubscription = eventBus.Subscribe<InvalidTxDetectedEvent>();

      // Create and submit first transaction
      var coin = availableCoins.Dequeue();
      var (txHex1, txId1) = CreateNewTransaction(coin, new Money(1000L));
      var response = await node0.RpcClient.SendRawTransactionAsync(HelperTools.HexStringToByteArray(txHex1), true, false, cts.Token);

      var curTxHex = txHex1;
      var curTxId = txId1;
      int dsCheckTxCount = 0;
      for (int i = 0; i < 100; i++)
      {
        Transaction.TryParse(curTxHex, Network.RegTest, out Transaction curTx);
        var curTxCoin = new Coin(curTx, 0);
        (curTxHex, curTxId) = CreateNewTransaction(curTxCoin, new Money(1000L));
        // Submit every 10th tx to mapi with dsCheck
        if (i % 10 == 0)
        {
          var payload = await SubmitTransactionAsync(curTxHex, false, true);
          Assert.AreEqual(payload.ReturnResult, "success");
          dsCheckTxCount++;
        }
        else
        {
          _ = await node0.RpcClient.SendRawTransactionAsync(HelperTools.HexStringToByteArray(curTxHex), true, false, cts.Token);
        }
      }

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
      Assert.AreEqual(dsCheckTxCount, calls.Length);
      var callback = HelperTools.JSONDeserialize<JSONEnvelopeViewModel>(calls[0].request)
        .ExtractPayload<CallbackNotificationDoubleSpendViewModel>();

      Assert.AreEqual(CallbackReason.DoubleSpendAttempt, callback.CallbackReason);
    }

    private async Task<(string, string)> CreateUnconfirmedAncestorChain(string txHex1, string txId1, int length, CancellationToken? cancellationToken = null)
    {
      var curTxHex = txHex1;
      var curTxId = txId1;
      for (int i = 0; i < length; i++)
      {
        Transaction.TryParse(curTxHex, Network.RegTest, out Transaction curTx);
        var curTxCoin = new Coin(curTx, 0);
        (curTxHex, curTxId) = CreateNewTransaction(curTxCoin, new Money(1000L));
        _ = await node0.RpcClient.SendRawTransactionAsync(HelperTools.HexStringToByteArray(curTxHex), true, false, cancellationToken);
      }

      return (curTxHex, curTxId);
    }
    
    [TestMethod]
    public async Task CatchAncestorDoubleSpendOfBlockTxByBlockTx()
    {
      using CancellationTokenSource cts = new CancellationTokenSource(cancellationTimeout);

      await RegisterNodesWithServiceAndWait(cts.Token);
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
      var (lastTxHex, lastTxId) = await CreateUnconfirmedAncestorChain(txHex1, txId1, 100, cts.Token);

      var parentBlockHash = await rpcClient0.GetBestBlockHashAsync();
      var parentBlockHeight = (await rpcClient0.GetBlockHeaderAsync(parentBlockHash)).Height;

      // Create second transaction using output from last tx in chain
      Transaction.TryParse(lastTxHex, Network.RegTest, out Transaction lastTx);
      var lastTxCoin = new Coin(lastTx, 0);
      var (txHex2, txId2) = CreateNewTransaction(lastTxCoin, new Money(1000L));
      var payload2 = await SubmitTransactionAsync(txHex2, true, true);
      Assert.AreEqual(payload2.ReturnResult, "success");

      // Mine a new block containing tx1
      var b1Hash = (await rpcClient0.GenerateAsync(1)).Single();

      WaitUntilEventBusIsIdle();

      var calls = Callback.Calls;
      Assert.AreEqual(1, calls.Length);
      var signedJSON = HelperTools.JSONDeserialize<SignedPayloadViewModel>(calls[0].request);
      var notification = HelperTools.JSONDeserialize<CallbackNotificationViewModelBase>(signedJSON.Payload);
      Assert.AreEqual(CallbackReason.MerkleProof, notification.CallbackReason);
      
      // Mine sibling block to b1 - without any additional transaction
      var (b2, _) = await MineNextBlockAsync(new Transaction[0], false, parentBlockHash);

      // Mine a child block to b2, containing tx2. This will create a longer chain and we should be notified about doubleSpend
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
      Assert.AreEqual(txId2, dsNotification.CallbackPayload.DoubleSpendTxId);
    }

  }
}
