// Copyright (c) 2020 Bitcoin Association

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using MerchantAPI.APIGateway.Domain.Actions;
using MerchantAPI.APIGateway.Domain.Models;
using MerchantAPI.APIGateway.Domain.Models.Events;
using MerchantAPI.APIGateway.Rest.Services;
using MerchantAPI.APIGateway.Rest.ViewModels;
using MerchantAPI.APIGateway.Test.Functional.Server;
using MerchantAPI.Common.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NBitcoin;
using NBitcoin.Altcoins;


namespace MerchantAPI.APIGateway.Test.Functional
{
  [TestClass]
  public class ZMQTests : TestBaseWithBitcoind
  {
    private int cancellationTimeout = 30000; // 30 seconds
    public ZMQSubscriptionService zmqService;

    [TestInitialize]
    public override void TestInitialize()
    {
      base.TestInitialize();
      zmqService = server.Services.GetRequiredService<ZMQSubscriptionService>();
      InsertFeeQuote();
    }


    [TestCleanup]
    public override void TestCleanup()
    {
      base.TestCleanup();
    }

    [TestMethod]
    public async Task UnsubscribeFromNodeOnNodeRemoval()
    {
      using CancellationTokenSource cts = new CancellationTokenSource(cancellationTimeout);

      var zmqUnsubscribedSubscription = eventBus.Subscribe<ZMQUnsubscribedEvent>();

      await RegisterNodesWithService(cts.Token);
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
      using CancellationTokenSource cts = new CancellationTokenSource(cancellationTimeout);

      await RegisterNodesWithService(cts.Token);
      Assert.AreEqual(1, zmqService.GetActiveSubscriptions().Count());

      // Subscribe new block events
      var newBlockDiscoveredSubscription = eventBus.Subscribe<NewBlockDiscoveredEvent>();

      WaitUntilEventBusIsIdle();

      // Mine one block
      var blockHash = await rpcClient0.GenerateAsync(1);
      Assert.AreEqual(1, blockHash.Length);

      // New block discovered event should be fired
      var newBlockArrivedSubscription = await newBlockDiscoveredSubscription.ReadAsync(cts.Token);
      Assert.AreEqual(blockHash[0], newBlockArrivedSubscription.BlockHash);
    }

    [TestMethod]
    public async Task CatchInMempoolDoubleSpendZMQMessage()
    {
      using CancellationTokenSource cts = new CancellationTokenSource(cancellationTimeout);

      await RegisterNodesWithService(cts.Token);
      Assert.AreEqual(1, zmqService.GetActiveSubscriptions().Count());

      // Subscribe invalidtx events
      var invalidTxDetectedSubscription = eventBus.Subscribe<InvalidTxDetectedEvent>();

      // Create two transactions from same input
      var coin = availableCoins.Dequeue();
      var (txHex1, txId1) = CreateNewTransaction(coin, new Money(1000L));
      var (txHex2, txId2) = CreateNewTransaction(coin, new Money(500L));

      // Transactions should not be the same
      Assert.AreNotEqual(txHex1, txHex2);

      // Send first transaction using MAPI
      var payload = await SubmitTransaction(txHex1);
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

      WaitUntilEventBusIsIdle();

      // One notification should be present
      var doubleSpends = (await TxRepositoryPostgres.GetTxsToSendMempoolDSNotificationsAsync()).ToArray(); 
      Assert.AreEqual(1, doubleSpends.Count());
      Assert.AreEqual(new uint256(txId1), new uint256(doubleSpends.First().TxExternalId));
      Assert.AreEqual(new uint256(txId2), new uint256(doubleSpends.First().DoubleSpendTxId));

    }

    (string txHex, string txId) CreateNewTransaction(Coin coin, Money amount)
    { 
      var address = BitcoinAddress.Create(testAddress, Network.RegTest);
      var tx = BCash.Instance.Regtest.CreateTransaction();

      tx.Inputs.Add(new TxIn(coin.Outpoint));
      tx.Outputs.Add(coin.Amount - amount, address);

      var key = Key.Parse(testPrivateKeyWif, Network.RegTest);

      tx.Sign(key.GetBitcoinSecret(Network.RegTest), coin);

      return (tx.ToHex(), tx.GetHash().ToString());
    }


    async Task<SubmitTransactionResponseViewModel> SubmitTransaction(string txHex)
    {

      // Send transaction
      var callbackUrl = "http://www.something.com";
      var reqContent = new StringContent($"{{ \"rawtx\": \"{txHex}\", \"dscheck\": true, \"CallBackUrl\": \"{callbackUrl}\",  \"CallBackToken\": \"xxx\"}}");
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);      
      var response =
        await Post<SignedPayloadViewModel>(MapiServer.ApiMapiSubmitTransaction, client, reqContent, HttpStatusCode.OK);

      return response.response.ExtractPayload<SubmitTransactionResponseViewModel>();

    }


    private async Task RegisterNodesWithService(CancellationToken cancellationToken)
    {
      var subscribedToZMQSubscription = eventBus.Subscribe<ZMQSubscribedEvent>();
      
      // Register nodes with service
      var nodes = this.NodeRepository.GetNodes();
      foreach (var node in nodes)
      {
        eventBus.Publish(new NodeAddedEvent { CreatedNode = node });
      }

      // Wait for subscription event so we can make sure that service is listening to node
      _ = await subscribedToZMQSubscription.ReadAsync(cancellationToken);

      // Unsubscribe from event bus
      eventBus.TryUnsubscribe(subscribedToZMQSubscription);
    }

    [TestMethod]
    public async Task BlockInfoIsUpToDate()
    {
      // wait until we are subscribed to ZMq notification
      await WaitUntilAsync(() => zmqService.GetActiveSubscriptions().Any());
      WaitUntilEventBusIsIdle();

      var info = blockChainInfo.GetInfo();
      var newBlockHash = (await rpcClient0.GenerateAsync(1))[0];
      Assert.AreNotEqual(info.BestBlockHash, newBlockHash[0], "New block should have been mined");
      loggerTest.LogInformation($"We mined a new block {newBlockHash}. Checking if  GetInfo() reports it");
      await WaitUntilAsync(() => blockChainInfo.GetInfo().BestBlockHash == newBlockHash);
    }
  }
}
