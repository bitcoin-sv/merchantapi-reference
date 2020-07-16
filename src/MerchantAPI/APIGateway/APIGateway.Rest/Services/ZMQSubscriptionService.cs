// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Linq;
using Microsoft.Extensions.Logging;
using NetMQ;
using NetMQ.Sockets;
using MerchantAPI.APIGateway.Domain.Models;
using MerchantAPI.APIGateway.Domain.Models.Events;
using MerchantAPI.Common.BitcoinRpc;
using MerchantAPI.Common;
using MerchantAPI.APIGateway.Domain.Repositories;
using MerchantAPI.Common.EventBus;
using MerchantAPI.Common.Json;
using System.Text.Json;
using MerchantAPI.APIGateway.Domain.Models.Zmq;

namespace MerchantAPI.APIGateway.Rest.Services
{
  public class ZMQSubscriptionService : BackgroundServiceWithSubscriptions<ZMQSubscriptionService>
  {
    private readonly INodeRepository nodeRepository;
    private readonly IRpcClientFactory bitcoindFactory;

    private readonly ConcurrentDictionary<string, ZMQSubscription> subscriptions =
      new ConcurrentDictionary<string, ZMQSubscription>();

    private readonly List<Node> nodesAdded = new List<Node>();
    private readonly List<Node> nodesDeleted = new List<Node>();
    private EventBusSubscription<NodeAddedEvent> nodeAddedSubscription;
    private EventBusSubscription<NodeDeletedEvent> nodeDeletedSubscription;

    public ZMQSubscriptionService(ILogger<ZMQSubscriptionService> logger,
      INodeRepository nodeRepository,
      IEventBus eventBus,
      IRpcClientFactory bitcoindFactory)
      : base(logger, eventBus)
    {
      this.nodeRepository = nodeRepository ?? throw new ArgumentNullException(nameof(nodeRepository));
      this.bitcoindFactory = bitcoindFactory ?? throw new ArgumentNullException(nameof(bitcoindFactory));
    }


    private Task NodeRepositoryNodeAddedAsync(NodeAddedEvent e)
    {
      lock (nodesAdded)
      {
        nodesAdded.Add(e.CreatedNode);
      }

      return Task.CompletedTask;
    }

    private Task NodeRepositoryDeletedEventAsync(NodeDeletedEvent e)
    {
      lock (nodesDeleted)
      {
        nodesDeleted.Add(e.DeletedNode);
      }

      return Task.CompletedTask;
    }

    protected override void SubscribeToEventBus(CancellationToken stoppingToken)
    {
      // subscribe to node events 
      nodeAddedSubscription = eventBus.Subscribe<NodeAddedEvent>();
      nodeDeletedSubscription = eventBus.Subscribe<NodeDeletedEvent>();
      _ = nodeAddedSubscription.ProcessEventsAsync(stoppingToken, logger, NodeRepositoryNodeAddedAsync);
      _ = nodeDeletedSubscription.ProcessEventsAsync(stoppingToken, logger, NodeRepositoryDeletedEventAsync);
    }

    protected override Task ProcessMissedEvents()
    {
      lock (nodesAdded)
      {
        // Add existing nodes from repository
          nodesAdded.AddRange(nodeRepository.GetNodes());
      }

      // Nothing to to here, we do not have persistent ZMQ queue
      return Task.CompletedTask;
    }

    protected override void UnsubscribeFromEventBus()
    {
      eventBus?.TryUnsubscribe(nodeAddedSubscription);
      nodeAddedSubscription = null;
      eventBus?.TryUnsubscribe(nodeDeletedSubscription);
      nodeDeletedSubscription = null;
    }

    protected override async Task ExecuteActualWorkAsync(CancellationToken stoppingToken)
    {
      while (!stoppingToken.IsCancellationRequested)
      {
        // First check for repository event, so that we subscribe as fast as possible at startup
        await ProcessNodeRepositoryEvents(stoppingToken);

        if (subscriptions.Count > 0)
        {
          foreach (var subscription in subscriptions.Values)
          {
            await ProcessSubscription(subscription, stoppingToken);
          }
        }
        else
        {
          await Task.Delay(100, stoppingToken);
        }

      }
    }

    private Task ProcessSubscription(ZMQSubscription subscription, CancellationToken stoppingToken)
    {
      if (subscription.Socket.TryReceiveFrameString(TimeSpan.FromMilliseconds(100), out string msgTopic))
      {
        var msg = subscription.Socket.ReceiveMultipartBytes();
        logger.LogDebug($"Received message with topic {msgTopic}. Length: {msg.Count}");
        switch(msgTopic)
        {
          case ZMQTopic.HashBlock:
            string blockHash = HelperTools.ByteToHexString(msg[0]);
            logger.LogInformation($"New block with hash {blockHash}.");
            eventBus.Publish(new NewBlockDiscoveredEvent { BlockHash = blockHash });
            break;
          case ZMQTopic.InvalidTx:
            var invalidTxMsg = JsonSerializer.Deserialize<InvalidTxMessage>(msg[0]);
            logger.LogInformation($"Invalid tx notification for tx {invalidTxMsg.TxId} with reason {invalidTxMsg.RejectionCode} - {invalidTxMsg.RejectionReason}.");
            eventBus.Publish(new InvalidTxDetectedEvent { Message = invalidTxMsg }); 
            break;
          default:
            logger.LogInformation($"Unknown message topic {msgTopic} received. Ignoring.");
            break;
        }
      }
      return Task.CompletedTask;
    }

    private async Task ProcessNodeRepositoryEvents(CancellationToken? stoppingToken = null)
    {
      bool nodesRepositoryChanged = false;
      // Copy to local list
      Node[] nodesAddedLocal;
      lock (nodesAdded)
      {
        nodesAddedLocal = nodesAdded.ToArray();
        nodesAdded.Clear();
      }

      if (nodesAddedLocal.Any())
      {
        logger.LogInformation($"{nodesAddedLocal.Length} new nodes were added to repository. Will activate ZMQ subscriptions");
        nodesRepositoryChanged = true;
      }
      // Subscribe to new nodes
      foreach (var node in nodesAddedLocal)
      {
        // Try to connect to node to get list of available events
        var bitcoind = bitcoindFactory.Create(node.Host, node.Port, node.Username, node.Password);
        try
        {
          // try to call some method to test if connectivity parameters are correct
          
          var notifications = await bitcoind.ActiveZmqNotificationsAsync(stoppingToken);
          foreach (var notification in notifications)
          {
            string topic = notification.Notification.Substring(3); // Chop of "zmq" prefix
            if (topic == ZMQTopic.HashBlock ||
              topic == ZMQTopic.InvalidTx ||
              topic == ZMQTopic.RemovedFromMempool ||
              topic == ZMQTopic.RemovedFromMempoolBlock)
            {
              SubscribeTopic(node.Id, notification.Address, topic);
            }
          }
          eventBus.Publish(new ZMQSubscribedEvent { SourceNode = node });
        }
        catch (Exception ex)
        {
          throw new BadRequestException($"Cannot subscribe to ZMQ events. Unable to connect to node {node.Host}:{node.Port}.", ex);
        }
      }

      // Copy to local list
      Node[] nodesDeletedLocal;
      lock (nodesDeleted)
      {
        nodesDeletedLocal = nodesDeleted.ToArray();
        nodesDeleted.Clear();
      }

      if (nodesDeletedLocal.Any())
      {
        logger.LogInformation($"{nodesDeletedLocal.Length} new nodes were removed from repository. Will remove ZMQ subscriptions");
        nodesRepositoryChanged = true;
      }

      // Remove deleted nodes from subscriptions
      foreach (var node in nodesDeletedLocal)
      {
        var subscriptionsToRemove = subscriptions.Where(s => s.Value.NodeId == node.Id);
        foreach (var subscription in subscriptionsToRemove)
        {
          subscriptions.TryRemove(subscription.Key, out ZMQSubscription val);
          val?.Socket.Close();          
        }
        eventBus.Publish(new ZMQUnsubscribedEvent { SourceNode = node });
      }

      if (nodesRepositoryChanged)
      {
        logger.LogInformation($"There are now {subscriptions.Count} active subscriptions after updates to node list were processed");
      }
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
      logger.LogInformation("ZMQSubscriptionService is starting.");
      return base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken stoppingToken)
    {
      logger.LogInformation("ZMQSubscriptionService is stopping.");

      subscriptions.Clear();

      return base.StopAsync(stoppingToken);
    }

    public IEnumerable<ZMQSubscription> GetActiveSubscriptions()
    {
      return subscriptions.Values;
    }

    /// <summary>
    /// Method subscribes to ZMQ topic. It opens new connection if one does not exist yet
    /// </summary>
    private void SubscribeTopic(long nodeId, string address, string topic)
    {
      if (subscriptions.ContainsKey(address))
      {
        if (!subscriptions[address].IsTopicSubscribed(topic))
        {
          subscriptions[address].SubscribeTopic(topic);
        }
      }
      else
      {
        subscriptions.TryAdd(address, new ZMQSubscription(nodeId, address, topic));
      }
    }
  }

  static class ZMQTopic
  {
    public const string HashBlock = "hashblock";
    public const string InvalidTx = "invalidtx";
    public const string RemovedFromMempool = "removedfrommempool";
    public const string RemovedFromMempoolBlock = "removedfrommempoolblock";
  }

  public class ZMQSubscription : IDisposable
  {
    private readonly List<string> topics = new List<string>();

    public ZMQSubscription(long nodeId, string address, string topic = null)
    {
      NodeId = nodeId;
      Address = address;
      Socket = new SubscriberSocket();
      Socket.Connect(address);
      if (topic != null)
      {
        SubscribeTopic(topic);
      }
    }

    public void SubscribeTopic(string topic)
    {
      Socket.Subscribe(topic);
      topics.Add(topic);
    }

    public bool IsTopicSubscribed(string topic)
    {
      return topics.Contains(topic);
    }

    void IDisposable.Dispose()
    {
      Socket.Dispose();
    }

    public long NodeId { get; }

    public string Address { get; }

    public SubscriberSocket Socket { get; }
  }
}
