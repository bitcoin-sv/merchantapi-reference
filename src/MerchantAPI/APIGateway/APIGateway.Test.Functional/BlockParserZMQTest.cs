// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain.Models.Events;
using MerchantAPI.APIGateway.Rest.Services;
using MerchantAPI.Common.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NBitcoin;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;


namespace MerchantAPI.APIGateway.Test.Functional
{
  [TestCategory("TestCategoryNo2")]
  [TestClass]
  public class BlockParserZMQTest : BlockParserTestBase
  {
    readonly int cancellationTimeout = 30000; // 30 seconds
    ZMQSubscriptionService zmqService;

    [TestInitialize]
    override public void TestInitialize()
    {
      base.TestInitialize();
      zmqService = server.Services.GetRequiredService<ZMQSubscriptionService>();
    }

    [TestCleanup]
    override public void TestCleanup()
    {
      base.TestCleanup();
    }

    private async Task RegisterNodesWithServiceAndWait(CancellationToken cancellationToken)
    {
      var subscribedToZMQSubscription = EventBus.Subscribe<ZMQSubscribedEvent>();

      // Register nodes with service
      RegisterNodesWithService();

      // Wait for subscription event so we can make sure that service is listening to node
      _ = await subscribedToZMQSubscription.ReadAsync(cancellationToken);

      // Unsubscribe from event bus
      EventBus.TryUnsubscribe(subscribedToZMQSubscription);
    }

    private void RegisterNodesWithService()
    {
      // Register all nodes with service
      var nodes = this.NodeRepository.GetNodes();
      foreach (var node in nodes)
      {
        EventBus.Publish(new NodeAddedEvent() { CreationDate = DateTime.UtcNow, CreatedNode = node });
      }
    }

    [TestMethod]
    public async Task CatchBlockHashZMQMessage()
    {
      using CancellationTokenSource cts = new(cancellationTimeout);

      await RegisterNodesWithServiceAndWait(cts.Token);
      Assert.AreEqual(1, zmqService.GetActiveSubscriptions().Count());

      // Subscribe new block events
      var newBlockDiscoveredSubscription = EventBus.Subscribe<NewBlockDiscoveredEvent>();

      WaitUntilEventBusIsIdle();

      // Mine one block
      var node = NodeRepository.GetNodes().First();
      var rpcClient = rpcClientFactoryMock.Create(node.Host, node.Port, node.Username, node.Password);
      var tx = Transaction.Parse(Tx1Hex, Network.Main);
      var (blockCount, blockHash) = await CreateAndPublishNewBlockAsync(rpcClient, null, tx);
      Assert.IsNotNull(blockHash);

      // New block discovered events should be fired
      // block0 is published on first call to CreateAndPublishNewBlock
      var newBlockArrivedSubscription = await newBlockDiscoveredSubscription.ReadAsync(cts.Token);
      // block1
      newBlockArrivedSubscription = await newBlockDiscoveredSubscription.ReadAsync(cts.Token);
      Assert.AreEqual(blockHash, newBlockArrivedSubscription.BlockHash);
    }

    [TestMethod]
    public async Task QueueProcessingAfterOperationCancelled()
    {
      using CancellationTokenSource cts = new(cancellationTimeout);

      await RegisterNodesWithServiceAndWait(cts.Token);
      Assert.AreEqual(1, zmqService.GetActiveSubscriptions().Count());

      // Subscribe new block events
      var newBlockDiscoveredSubscription = EventBus.Subscribe<NewBlockDiscoveredEvent>();

      WaitUntilEventBusIsIdle();

      // Mine one block
      var node = NodeRepository.GetNodes().First();

      var rpcClient = rpcClientFactoryMock.Create(node.Host, node.Port, node.Username, node.Password);
      var tx = Transaction.Parse(Tx1Hex, Network.Main);

      var (blockCount1, blockHash1) = await CreateAndPublishNewBlockAsync(rpcClient, null, tx);
      Assert.IsNotNull(blockHash1);

      // block0
      var newBlockArrivedSubscription = await newBlockDiscoveredSubscription.ReadAsync(cts.Token);
      // block1
      newBlockArrivedSubscription = await newBlockDiscoveredSubscription.ReadAsync(cts.Token);
      Assert.AreEqual(blockHash1, newBlockArrivedSubscription.BlockHash);
      Assert.IsTrue(HelperTools.AreByteArraysEqual(new uint256(blockHash1).ToBytes(), (await TxRepositoryPostgres.GetBestBlockAsync()).BlockHash));

      // trigger OperationCanceledException
      rpcClientFactoryMock.SetUpPredefinedResponse(("mocknode0:getblockheader", new OperationCanceledException()));
      var tx2 = Transaction.Parse(Tx2Hex, Network.Main);
      var (_, blockHash2) = await CreateAndPublishNewBlockAsync(rpcClient, null, tx2);
      Assert.IsNotNull(blockHash2);

      // block2 arrives, but is not saved to db because of error
      newBlockArrivedSubscription = await newBlockDiscoveredSubscription.ReadAsync(cts.Token);
      Assert.AreEqual(blockHash2, newBlockArrivedSubscription.BlockHash);
      Assert.IsFalse(HelperTools.AreByteArraysEqual(new uint256(blockHash2).ToBytes(), (await TxRepositoryPostgres.GetBestBlockAsync()).BlockHash));

      // clear error triggering
      rpcClientFactoryMock.SetUpPredefinedResponse();

      // publish block3
      var (blockCount3, blockHash3) = await CreateAndPublishNewBlockAsync(rpcClient, null, tx2);
      newBlockArrivedSubscription = await newBlockDiscoveredSubscription.ReadAsync(cts.Token);
      Assert.AreEqual(blockHash3, newBlockArrivedSubscription.BlockHash);
      Assert.AreNotEqual(blockHash3, blockHash2);

      // all blocks are now present in db
      Assert.IsNotNull(await TxRepositoryPostgres.GetBlockAsync(new uint256(blockHash2).ToBytes()));
      var dbBlock = await TxRepositoryPostgres.GetBestBlockAsync();
      Assert.IsTrue(HelperTools.AreByteArraysEqual(new uint256(blockHash3).ToBytes(), dbBlock.BlockHash));
      Assert.AreEqual(3, dbBlock.BlockHeight.Value);
    }
  }
}
