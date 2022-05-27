// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain;
using MerchantAPI.APIGateway.Domain.Actions;
using MerchantAPI.APIGateway.Domain.Models;
using MerchantAPI.APIGateway.Domain.Models.Events;
using MerchantAPI.APIGateway.Domain.ViewModels;
using MerchantAPI.APIGateway.Rest.ViewModels;
using MerchantAPI.APIGateway.Test.Functional.Server;
using MerchantAPI.Common.Json;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NBitcoin;
using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;


namespace MerchantAPI.APIGateway.Test.Functional
{
  [TestCategory("TestCategoryNo2")]
  [TestClass]
  public class ZMQTests : ZMQTestBase
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
    public async Task UnsubscribeFromNodeOnNodeRemoval()
    {
      using CancellationTokenSource cts = new(cancellationTimeout);

      var zmqUnsubscribedSubscription = EventBus.Subscribe<ZMQUnsubscribedEvent>();

      await RegisterNodesWithServiceAndWaitAsync(cts.Token);
      Assert.AreEqual(1, zmqService.GetActiveSubscriptions().Count());

      // Delete one node and check that event is fired
      Nodes.DeleteNode(NodeRepository.GetNodes().First().ToExternalId());

      // Wait for subscription event so we can make sure that service is listening to node
      _ = await zmqUnsubscribedSubscription.ReadAsync(cts.Token);
      Assert.AreEqual(0, zmqService.GetActiveSubscriptions().Count());
    }

    [TestMethod]
    public async Task CatchBlockHashZMQMessage()
    {
      using CancellationTokenSource cts = new(cancellationTimeout);

      await RegisterNodesWithServiceAndWaitAsync(cts.Token);
      Assert.AreEqual(1, zmqService.GetActiveSubscriptions().Count());

      // Subscribe new block events
      var newBlockDiscoveredSubscription = EventBus.Subscribe<NewBlockDiscoveredEvent>();

      WaitUntilEventBusIsIdle();

      // Mine one block
      var blockHash = await rpcClient0.GenerateAsync(1);
      Assert.AreEqual(1, blockHash.Length);

      // New block discovered event should be fired
      var newBlockArrivedSubscription = await newBlockDiscoveredSubscription.ReadAsync(cts.Token);
      Assert.AreEqual(blockHash[0], newBlockArrivedSubscription.BlockHash);
    }

    private async Task<(string, string)> CatchInMempoolDoubleSpendZMQMessage()
    {
      using CancellationTokenSource cts = new(cancellationTimeout);

      await RegisterNodesWithServiceAndWaitAsync(cts.Token);
      Assert.AreEqual(1, zmqService.GetActiveSubscriptions().Count());

      // Subscribe invalidtx events
      var invalidTxDetectedSubscription = EventBus.Subscribe<InvalidTxDetectedEvent>();

      // Create two transactions from same input
      var coin = availableCoins.Dequeue();
      var (txHex1, txId1) = CreateNewTransaction(coin, new Money(1000L));
      var (txHex2, txId2) = CreateNewTransaction(coin, new Money(500L));

      // Transactions should not be the same
      Assert.AreNotEqual(txHex1, txHex2);

      // Send first transaction using MAPI
      var payload = await SubmitTransactionAsync(txHex1, true, true);
      Assert.AreEqual(payload.ReturnResult, "success");

      // Send second transaction using RPC
      try
      {
        _ = await node0.RpcClient.SendRawTransactionAsync(HelperTools.HexStringToByteArray(txHex2), true, false, cts.Token);
      }
      catch (Exception rpcException)
      {
        // Double spend will throw txn-mempool-conflict exception
        Assert.AreEqual("258: txn-mempool-conflict", rpcException.Message);
      }

      // InvalidTx event should be fired
      var invalidTxEvent = await invalidTxDetectedSubscription.ReadAsync(cts.Token);
      Assert.AreEqual(InvalidTxRejectionCodes.TxMempoolConflict, invalidTxEvent.Message.RejectionCode);
      Assert.AreEqual(txId2, invalidTxEvent.Message.TxId);
      Assert.IsNotNull(invalidTxEvent.Message.CollidedWith, "bitcoind did not return CollidedWith");
      Assert.AreEqual(1, invalidTxEvent.Message.CollidedWith.Length);
      Assert.AreEqual(txId1, invalidTxEvent.Message.CollidedWith[0].TxId);

      await CheckCallbacksAsync(1, cts.Token);

      // Check if callback was received
      var calls = Callback.Calls;
      var callback = HelperTools.JSONDeserialize<JSONEnvelopeViewModel>(calls[0].request)
        .ExtractPayload<CallbackNotificationDoubleSpendViewModel>();

      Assert.AreEqual(CallbackReason.DoubleSpendAttempt, callback.CallbackReason);
      Assert.AreEqual(-1, callback.BlockHeight);
      Assert.AreEqual(new uint256(txId1), new uint256(callback.CallbackTxId));
      Assert.AreEqual(new uint256(txId2), new uint256(callback.CallbackPayload.DoubleSpendTxId));

      return (txHex1, txHex2);
    }

    [TestMethod]
    public async Task CatchInMempoolDoubleSpendZMQMessageTest()
    {
      await CatchInMempoolDoubleSpendZMQMessage();
    }

    [TestMethod]
    public async Task CatchMempoolAndBlockDoubleSpendMessages()
    {
      using CancellationTokenSource cts = new(cancellationTimeout);

      var txs = await CatchInMempoolDoubleSpendZMQMessage();
      
      var tx1 = HelperTools.ParseBytesToTransaction(HelperTools.HexStringToByteArray(txs.Item1));
      var tx2 = HelperTools.ParseBytesToTransaction(HelperTools.HexStringToByteArray(txs.Item2));
      var txId1 = tx1.GetHash().ToString();
      var txId2 = tx2.GetHash().ToString();
      await MineNextBlockAsync(new[] { tx2 });

      var mempoolTxs2 = await rpcClient0.GetRawMempool();

      // Tx should no longer be in mempool
      Assert.IsFalse(mempoolTxs2.Contains(txId1), "Submitted tx1 should not be found in mempool");

      await CheckCallbacksAsync(2, cts.Token);

      var calls = Callback.Calls;
      var callbackDS = HelperTools.JSONDeserialize<JSONEnvelopeViewModel>(calls[1].request)
        .ExtractPayload<CallbackNotificationDoubleSpendViewModel>();
      Assert.AreEqual(CallbackReason.DoubleSpend, callbackDS.CallbackReason);
      Assert.AreEqual(new uint256(txId1), new uint256(callbackDS.CallbackTxId));
      Assert.AreEqual(new uint256(txId2), new uint256(callbackDS.CallbackPayload.DoubleSpendTxId));

    }

    [TestMethod]
    public async Task CatchDoubleSpendOfMempoolTxByBlockTx()
    {
      using CancellationTokenSource cts = new(cancellationTimeout);

      // Create two transactions from same input
      var coin = availableCoins.Dequeue();
      var (txHex1, txId1) = CreateNewTransaction(coin, new Money(1000L));
      var (txHex2, txId2) = CreateNewTransaction(coin, new Money(500L));

      
      var tx2 = HelperTools.ParseBytesToTransaction(HelperTools.HexStringToByteArray(txHex2));
      // Transactions should not be the same
      Assert.AreNotEqual(txHex1, txHex2);

      // Send first transaction using mAPI
      var payload = await SubmitTransactionAsync(txHex1, true, true);
      Assert.AreEqual("success", payload.ReturnResult);

      var mempoolTxs = await rpcClient0.GetRawMempool();

      // Transactions should be in mempool 
      Assert.IsTrue(mempoolTxs.Contains(txId1), "Submitted tx1 not found in mempool");
      
      Assert.AreEqual(0, Callback.Calls.Length);

      // Mine a new block containing tx2
      await MineNextBlockAsync(new[] {tx2});

      var mempoolTxs2 = await rpcClient0.GetRawMempool();

      // Tx should no longer be in mempool
      Assert.IsFalse(mempoolTxs2.Contains(txId1), "Submitted tx1 should not be found in mempool");

      await CheckCallbacksAsync(1, cts.Token);

      var calls = Callback.Calls;
      var callback = HelperTools.JSONDeserialize<JSONEnvelopeViewModel>(calls[0].request)
        .ExtractPayload<CallbackNotificationDoubleSpendViewModel>();

      Assert.AreEqual(CallbackReason.DoubleSpend, callback.CallbackReason);
      Assert.AreEqual(new uint256(txId1), new uint256(callback.CallbackTxId));
      Assert.AreEqual(new uint256(txId2), new uint256(callback.CallbackPayload.DoubleSpendTxId));

    }

    [TestMethod]
    public async Task CatchDoubleSpendOfBlockTxByBlockTx()
    {
      using CancellationTokenSource cts = new(cancellationTimeout);

      // Create two transactions from same input
      var coin = availableCoins.Dequeue();
      var (txHex1, _) = CreateNewTransaction(coin, new Money(1000L));
      var (txHex2, txId2) = CreateNewTransaction(coin, new Money(500L));


      var tx1 = HelperTools.ParseBytesToTransaction(HelperTools.HexStringToByteArray(txHex1));
      var tx2 = HelperTools.ParseBytesToTransaction(HelperTools.HexStringToByteArray(txHex2));
      // Transactions should not be the same
      Assert.AreNotEqual(txHex1, txHex2);

      var parentBlockHash = await rpcClient0.GetBestBlockHashAsync();
      var parentBlockHeight = (await rpcClient0.GetBlockHeaderAsync(parentBlockHash)).Height;

      // Send first transaction using mAPI - we want to get DS notification for it 
      var payload = await SubmitTransactionAsync(txHex1, true, true);
      Assert.AreEqual(payload.ReturnResult, "success");

      // Mine a new block containing tx1
      var b1Hash = (await rpcClient0.GenerateAsync(1)).Single();


      loggerTest.LogInformation($"Block b1 {b1Hash} was mined containing tx1 {tx1.GetHash()}");

      await CheckCallbacksAsync(1, cts.Token);

      var calls = Callback.Calls;
      var signedJSON = HelperTools.JSONDeserialize<SignedPayloadViewModel>(calls[0].request);
      var notification = HelperTools.JSONDeserialize<CallbackNotificationViewModelBase>(signedJSON.Payload);
      Assert.AreEqual(CallbackReason.MerkleProof, notification.CallbackReason);

      // Mine sibling block to b1 - without any additional transaction
      var (b2,_) = await MineNextBlockAsync(Array.Empty<Transaction>(), false, parentBlockHash);

      loggerTest.LogInformation($"Block b2 {b2.Header.GetHash()} was mined with only coinbase transaction");

      // Mine a child block to b2, containing tx2. This will create a longer chain and we should be notified about doubleSpend
      var (b3, _ ) = await MineNextBlockAsync(new [] {tx2}, true, b2, parentBlockHeight+2);

      loggerTest.LogInformation($"Block b3 {b3.Header.GetHash()} was mined with a ds transaction tx2 {tx2.GetHash()}");

      // Check if b3 was accepted
      var currentBestBlock = await rpcClient0.GetBestBlockHashAsync();
      Assert.AreEqual(b3.GetHash().ToString(), currentBestBlock , "b3 was not activated");

      await CheckCallbacksAsync(2, cts.Token);

      calls = Callback.Calls;
      signedJSON = HelperTools.JSONDeserialize<SignedPayloadViewModel>(calls[1].request);
      var dsNotification = HelperTools.JSONDeserialize<CallbackNotificationDoubleSpendViewModel>(signedJSON.Payload);
      Assert.AreEqual(CallbackReason.DoubleSpend, dsNotification.CallbackReason);
      Assert.AreEqual(txId2, dsNotification.CallbackPayload.DoubleSpendTxId);
    }

    [TestMethod]
    public async Task ReceiveZMQMessagesAfterNodeRestart()
    {
      using CancellationTokenSource cts = new(cancellationTimeout);

      await RegisterNodesWithServiceAndWaitAsync(cts.Token);
      Assert.AreEqual(1, zmqService.GetActiveSubscriptions().Count());

      // Subscribe zmq subscribe, unsubscribe and new block events
      var subscribedToZMQSubscription = EventBus.Subscribe<ZMQSubscribedEvent>();
      var unscubscribeToZMQSubscription = EventBus.Subscribe<ZMQUnsubscribedEvent>();
      var newBlockDiscoveredSubscription = EventBus.Subscribe<NewBlockDiscoveredEvent>();

      WaitUntilEventBusIsIdle();

      // Mine one block
      var blockHash = await rpcClient0.GenerateAsync(1);
      Assert.AreEqual(1, blockHash.Length);

      // New block discovered event should be fired
      var firstNewBlockArrivedSubscription = await newBlockDiscoveredSubscription.ReadAsync(cts.Token);
      Assert.AreEqual(blockHash[0], firstNewBlockArrivedSubscription.BlockHash);

      // Stop bitcoind service
      StopBitcoind(node0);

      _ = await unscubscribeToZMQSubscription.ReadAsync(cts.Token);
      WaitUntilEventBusIsIdle();

      // Start bitcoind service
      StartBitcoind(0);

      _ = await subscribedToZMQSubscription.ReadAsync(cts.Token);
      WaitUntilEventBusIsIdle();

      // Mine one block
      blockHash = await rpcClient0.GenerateAsync(1);
      Assert.AreEqual(1, blockHash.Length);

      // New block discovered event should be fired
      var secondNewBlockArrivedSubscription = await newBlockDiscoveredSubscription.ReadAsync(cts.Token);
      Assert.AreEqual(blockHash[0], secondNewBlockArrivedSubscription.BlockHash);
    }

    [TestMethod]
    [SkipNodeStart]
    public async Task SubscribeToZMQOnNodeStart()
    {
      using CancellationTokenSource cts = new(cancellationTimeout);

      // Subscribe to failed, subscription and new block events
      var subscribedToZMQFailed = EventBus.Subscribe<ZMQFailedEvent>();
      var subscribedToZMQSubscription = EventBus.Subscribe<ZMQSubscribedEvent>();
      var newBlockDiscoveredSubscription = EventBus.Subscribe<NewBlockDiscoveredEvent>();

      // Add node to database and emit repository event
      var node = new Node(0, "localhost", 18332, "user", "password", $"This is a test node #0",
        null, (int)NodeStatus.Connected, null, null);
      this.NodeRepository.CreateNode(node);
      EventBus.Publish(new NodeAddedEvent() { CreationDate = DateTime.UtcNow, CreatedNode = node });

      // Should receive failed event
      _ = await subscribedToZMQFailed.ReadAsync(cts.Token);

      // There should be no active subscriptions
      Assert.AreEqual(0, zmqService.GetActiveSubscriptions().Count());

      // Cleanup event bus
      WaitUntilEventBusIsIdle();

      // Start bitcoind service
      node0 = StartBitcoind(0);
      rpcClient0 = node0.RpcClient;

      // Should receive subscription event
      _ = await subscribedToZMQSubscription.ReadAsync(cts.Token);

      // There should be one active subscription
      Assert.AreEqual(1, zmqService.GetActiveSubscriptions().Count());

      // Cleanup event bus
      WaitUntilEventBusIsIdle();

      // Mine one block
      var blockHash = await rpcClient0.GenerateAsync(1);
      Assert.AreEqual(1, blockHash.Length);

      // New block discovered event should be fired
      var secondNewBlockArrivedSubscription = await newBlockDiscoveredSubscription.ReadAsync(cts.Token);
      Assert.AreEqual(blockHash[0], secondNewBlockArrivedSubscription.BlockHash);
    }

    [TestMethod]
    public async Task BlockInfoIsUpToDate()
    {
      // wait until we are subscribed to ZMq notification
      await WaitUntilAsync(() => zmqService.GetActiveSubscriptions().Any());
      WaitUntilEventBusIsIdle();

      var info = await BlockChainInfo.GetInfoAsync();
      var newBlockHash = (await rpcClient0.GenerateAsync(1))[0];
      Assert.AreNotEqual(info.BestBlockHash, newBlockHash[0], "New block should have been mined");
      loggerTest.LogInformation($"We mined a new block {newBlockHash}. Checking if  GetInfo() reports it");
      await WaitUntilAsync(async () => (await BlockChainInfo.GetInfoAsync()).BestBlockHash == newBlockHash);
    }

    [TestMethod]
    public async Task ZmqStatusReturnsStatusForLiveNode()
    {
      using CancellationTokenSource cts = new(cancellationTimeout);

      await RegisterNodesWithServiceAndWaitAsync(cts.Token);
      Assert.AreEqual(1, zmqService.GetActiveSubscriptions().Count());

      WaitUntilEventBusIsIdle();

      // Get zmq status 
      var response =
        await Get<ZmqStatusViewModelGet[]>(Client, MapiServer.ApiZmqStatusUrl, HttpStatusCode.OK);

      Assert.AreEqual(1, response.Length);
      Assert.AreEqual(true, response.First().IsResponding);
      Assert.AreEqual(1, response.First().Endpoints.Length);
      Assert.IsTrue(response.First().Endpoints.First().Topics.Contains(ZMQTopic.InvalidTx));
      Assert.IsTrue(response.First().Endpoints.First().Topics.Contains(ZMQTopic.HashBlock));
    }

    [TestMethod]
    public async Task ZmqStatusReturnsNotRespondingForShutdownNode()
    {
      using CancellationTokenSource cts = new(cancellationTimeout);

      var subscribedToZMQFailed = EventBus.Subscribe<ZMQFailedEvent>();

      await RegisterNodesWithServiceAndWaitAsync(cts.Token);
      Assert.AreEqual(1, zmqService.GetActiveSubscriptions().Count());

      WaitUntilEventBusIsIdle();

      // Get zmq status - node should be responding
      var response =
        await Get<ZmqStatusViewModelGet[]>(Client, MapiServer.ApiZmqStatusUrl, HttpStatusCode.OK);

      Assert.AreEqual(1, response.Length);
      Assert.AreEqual(true, response.First().IsResponding);

      // Stop node
      StopBitcoind(node0);

      // Should receive failed event
      _ = await subscribedToZMQFailed.ReadAsync(cts.Token);

      // Get zmq status again - node should be marked as not responding
      response =
        await Get<ZmqStatusViewModelGet[]>(Client, MapiServer.ApiZmqStatusUrl, HttpStatusCode.OK);

      Assert.AreEqual(1, response.Length);
      Assert.AreEqual(false, response.First().IsResponding);
    }

  }
}
