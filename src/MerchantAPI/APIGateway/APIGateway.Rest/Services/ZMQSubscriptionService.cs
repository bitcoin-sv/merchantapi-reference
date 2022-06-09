// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using NetMQ;
using NetMQ.Sockets;
using MerchantAPI.APIGateway.Domain.Models;
using MerchantAPI.APIGateway.Domain.Models.Events;
using MerchantAPI.Common.BitcoinRpc;
using MerchantAPI.APIGateway.Domain.Repositories;
using MerchantAPI.Common.EventBus;
using MerchantAPI.Common.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MerchantAPI.APIGateway.Domain;
using MerchantAPI.APIGateway.Domain.Models.Zmq;
using MerchantAPI.Common.BitcoinRpc.Responses;
using MerchantAPI.Common.Clock;

namespace MerchantAPI.APIGateway.Rest.Services
{
  public class ZMQSubscriptionService : BackgroundServiceWithSubscriptions<ZMQSubscriptionService>
  {
    private readonly AppSettings appSettings;
    private readonly INodeRepository nodeRepository;
    private readonly IRpcClientFactory bitcoindFactory;
    private readonly IClock clock;
    private readonly int rpcResponseTimeoutSec;

    private readonly ConcurrentDictionary<string, ZMQSubscription> subscriptions = new();

    private readonly List<Node> nodesAdded = new();
    private readonly List<Node> nodesDeleted = new();
    private readonly List<ZMQFailedSubscription> failedSubscriptions = new();

    private EventBusSubscription<NodeAddedEvent> nodeAddedSubscription;
    private EventBusSubscription<NodeDeletedEvent> nodeDeletedSubscription;

    public ZMQSubscriptionService(ILogger<ZMQSubscriptionService> logger,
      IOptionsMonitor<AppSettings> options,
      INodeRepository nodeRepository,
      IEventBus eventBus,
      IRpcClientFactory bitcoindFactory,
      IClock clock)
      : base(logger, eventBus)
    {
      appSettings = options.CurrentValue ?? throw new ArgumentNullException(nameof(options));
      this.nodeRepository = nodeRepository ?? throw new ArgumentNullException(nameof(nodeRepository));
      this.bitcoindFactory = bitcoindFactory ?? throw new ArgumentNullException(nameof(bitcoindFactory));
      this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
      rpcResponseTimeoutSec = appSettings.ZmqConnectionRpcResponseTimeoutSec.Value;
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
        try
        {
          // First check for repository event, so that we subscribe as fast as possible at startup
          await ProcessNodeRepositoryEventsAsync(stoppingToken);

          // Process failed subscribe events
          await ProcessFailedNodesAsync(stoppingToken);

          if (!subscriptions.IsEmpty)
          {
            // Ping nodes that haven't sent any messages for a long time
            await PingNodesAsync(stoppingToken);

            // Check for new zmq messages
            foreach (var subscription in subscriptions.Values)
            {
              await ProcessSubscriptionAsync(subscription);
            }
          }
          else
          {
            await Task.Delay(100, stoppingToken);
          }
        }
        catch(Exception ex)
        {
          logger.LogError(ex, "ZMQService error");
        }
      }
    }

    private Task ProcessSubscriptionAsync(ZMQSubscription subscription)
    {
      if (subscription.Socket.TryReceiveFrameString(TimeSpan.FromMilliseconds(100), out string msgTopic))
      {
        var msg = subscription.Socket.ReceiveMultipartBytes();
        
        logger.LogDebug($"Received message with topic {msgTopic}. Length: {msg.Count}");
        switch(msgTopic)
        {
          case ZMQTopic.HashBlock:
            string blockHash = HelperTools.ByteToHexString(msg[0]);
            logger.LogDebug($"New block with hash {blockHash}.");
            eventBus.Publish(new NewBlockDiscoveredEvent() { CreationDate = clock.UtcNow(), BlockHash = blockHash });
            break;
          
          case ZMQTopic.InvalidTx:
            var invalidTxMsg = JsonSerializer.Deserialize<InvalidTxMessage>(msg[0]);
            logger.LogInformation($"Invalid tx notification for tx {invalidTxMsg.TxId} with reason {invalidTxMsg.RejectionCode} - {invalidTxMsg.RejectionReason}.");
            eventBus.Publish(new InvalidTxDetectedEvent() { CreationDate = clock.UtcNow(), Message = invalidTxMsg }); 
            break;

          case ZMQTopic.DiscardedFromMempool:
            var removedFromMempoolMsg = JsonSerializer.Deserialize<RemovedFromMempoolMessage>(msg[0]);
            logger.LogInformation($"Removed from mempool tx notification for tx {removedFromMempoolMsg.TxId} with reason {removedFromMempoolMsg.Reason}. ColidedWith.TxId = {removedFromMempoolMsg.CollidedWith?.TxId}");
            eventBus.Publish(new RemovedFromMempoolEvent() { CreationDate = clock.UtcNow(), Message = removedFromMempoolMsg });
            break;

          default:
            logger.LogDebug($"Unknown message topic {msgTopic} received. Ignoring.");
            logger.LogDebug($"Message: {Encoding.UTF8.GetString(msg[0])}");
            break;
        }
        subscription.LastMessageAt = clock.UtcNow(); 
      }
      return Task.CompletedTask;
    }

    /// <summary>
    /// Call activezmqnotifications on nodes that haven't posted any events for a given period of time (ZmqConnectionTestIntervalSec config setting).
    /// </summary>
    private async Task PingNodesAsync(CancellationToken stoppingToken)
    {
      var nodesToPing = subscriptions
        .Where(s => (clock.UtcNow() - s.Value.LastContactAt).TotalSeconds >= appSettings.ZmqConnectionTestIntervalSec)
        .Select(s => s.Value.NodeId)
        .Distinct()
        .ToArray();

      if (nodesToPing.Any())
      {
        var nodes = nodeRepository.GetNodes().ToArray();
        foreach (long nodeId in nodesToPing)
        {
          var node = nodes.FirstOrDefault(n => n.Id == nodeId);
          if (node == null)
            continue;
          var bitcoind = CreateZMQSubscriptionRpcClient(node);
          try
          {
            // Call activezmqnotifications rpc method just to check that node is still responding.
            // No validation of the response is made here.
            var notifications = await bitcoind.ActiveZmqNotificationsAsync(stoppingToken);
            // If we got answer than update last message timestamp on all subscriptions of this node
            if (notifications.Any())
            {
              foreach (var subscription in subscriptions.Where(s => s.Value.NodeId == node.Id).ToArray())
              {
                subscription.Value.LastPingAt = clock.UtcNow();
              }
            }
          }
          catch (Exception ex)
          {
            // Call failed so put node on failed list
            MarkAsFailed(node, ex.Message);
            // Remove any active subscriptions because we need full reconnection to this node
            UnsubscribeZmqNotifications(node);
            logger.LogError(ex, $"Ping failed for node {node.Host}:{node.Port}. Will try to resubscribe.");
          }
        }
      }
    }

    /// <summary>
    /// Retry to subscribe to node zmq if it failed on previous attempt. Retry interval is ZmqConnectionTestIntervalSec config setting.
    /// </summary>
    private async Task ProcessFailedNodesAsync(CancellationToken? stoppingToken = null)
    {
      // Copy failed nodes to local list
      ZMQFailedSubscription[] failedSubscriptionsLocal; 
      lock (failedSubscriptions)
      {
        failedSubscriptionsLocal = failedSubscriptions
          .Where(s => (clock.UtcNow() - s.LastTryAt).TotalSeconds >= appSettings.ZmqConnectionTestIntervalSec)
          .ToArray();
      }

      // Subscribe to new nodes      
      foreach (var failedSubscription in failedSubscriptionsLocal)
      {
        // Try to connect to node to get list of available events
        var bitcoind = CreateZMQSubscriptionRpcClient(failedSubscription.Node);
        try
        {
          // Call activezmqnotifications rpc method to check that node responds
          var notifications = await bitcoind.ActiveZmqNotificationsAsync(stoppingToken);
          ValidateNotifications(notifications);
          SubscribeZmqNotifications(failedSubscription.Node, notifications);
          ClearFailed(failedSubscription.Node);
        }
        catch (Exception ex)
        {
          logger.LogError($"Failed to subscribe to ZMQ events. " +
            $"Unable to connect to node {failedSubscription.Node.Host}:{failedSubscription.Node.Port}. " +
            $"Will retry in {appSettings.ZmqConnectionTestIntervalSec} seconds. {ex.Message}");
          failedSubscription.LastError = ex.Message;
          failedSubscription.LastTryAt = clock.UtcNow();
        }
      }
    }

    /// <summary>
    /// Method handles node repository events (adding or removing a node) by subscribing / unsubscribing to zmq.
    /// </summary>
    private async Task ProcessNodeRepositoryEventsAsync(CancellationToken? stoppingToken = null)
    {
      bool nodesRepositoryChanged = false;      

      // Copy new nodes to local list
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
        var bitcoind = CreateZMQSubscriptionRpcClient(node);
        try
        {
          // Call activezmqnotifications rpc method
          var notifications = await bitcoind.ActiveZmqNotificationsAsync(stoppingToken);
          ValidateNotifications(notifications);
          SubscribeZmqNotifications(node, notifications);
        }
        catch (Exception ex)
        {
          // Subscription failed so put node on failed list and log
          MarkAsFailed(node, ex.Message);
          logger.LogError($"Failed to subscribe to ZMQ events. Unable to connect to node {node.Host}:{node.Port}. Will retry later. {ex.Message}");
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
        UnsubscribeZmqNotifications(node);
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

    public int GetFailedSubscriptionsCount()
    {
      lock (failedSubscriptions)
      {
        return failedSubscriptions.Count;
      }
    }

    public ZmqStatus GetStatusForNode(Node node)
    {
      // Check active subscriptions
      var nodeSubscriptions = subscriptions.Where(s => s.Value.NodeId == node.Id).Select(s => s.Value).ToArray();
      if (nodeSubscriptions.Length > 0)
      {
        return new ZmqStatus()
        {
          IsResponding = true,
          LastConnectionAttemptAt = null,
          LastError = null,
          Endpoints = nodeSubscriptions.Select(s => new ZmqEndpoint()
          {
            Address = s.Address,
            Topics = s.Topics,
            LastPingAt = s.LastPingAt,
            LastMessageAt = s.LastMessageAt
          }).ToArray()
        };
      }
      // Check for failed status
      ZMQFailedSubscription failedSubscription;
      lock (failedSubscriptions)
      {
        failedSubscription = failedSubscriptions.FirstOrDefault(f => f.Node.Id == f.Node.Id);
      }
      if (failedSubscription != null)
      {
        return new ZmqStatus()
        {
          IsResponding = false,
          LastConnectionAttemptAt = failedSubscription.LastTryAt,
          LastError = failedSubscription.LastError
        };
      }
      // Unknown status - could happen if the node was just added and node repository
      // events have not been processed by ZMQ service yet.
      return new ZmqStatus()
      {
        IsResponding = false,
        LastConnectionAttemptAt = null,
        LastError = "Unknown"
      };
    }

    private static void ValidateNotifications(RpcActiveZmqNotification[] notifications)
    {
      // Check that we have all required notifications
      if (!notifications.Any() || notifications.Select(x => x.Notification).Intersect(ZMQTopic.RequiredZmqTopics).Count() != ZMQTopic.RequiredZmqTopics.Length)
      {
        var missingNotifications = ZMQTopic.RequiredZmqTopics.Except(notifications.Select(x => x.Notification));
        throw new Exception($"Node does not have all required zmq notifications enabled. Missing notifications ({string.Join(",", missingNotifications)})");
      }
    }

    private void SubscribeZmqNotifications(Node node, RpcActiveZmqNotification[] activeZmqNotifications)
    {
      foreach (var notification in activeZmqNotifications)
      {
        string topic = notification.Notification[3..]; // Chop off "pub" prefix
        if (topic == ZMQTopic.HashBlock ||
          topic == ZMQTopic.InvalidTx ||
          topic == ZMQTopic.DiscardedFromMempool)
        {
          // Use endpoint returned by rpc method if there is no endpoint configured for this node
          if (string.IsNullOrEmpty(node.ZMQNotificationsEndpoint))
          {
            SubscribeTopic(node.Id, notification.Address, topic);
          }
          else
          {
            SubscribeTopic(node.Id, node.ZMQNotificationsEndpoint, topic);
          }
        }
      }
      eventBus.Publish(new ZMQSubscribedEvent() { CreationDate = clock.UtcNow(), SourceNode = node });
    }

    private void UnsubscribeZmqNotifications(Node node)
    {
      var subscriptionsToRemove = subscriptions.Where(s => s.Value.NodeId == node.Id);
      foreach (var subscription in subscriptionsToRemove)
      {
        subscriptions.TryRemove(subscription.Key, out ZMQSubscription val);
        val?.Socket.Close();
      }
      eventBus.Publish(new ZMQUnsubscribedEvent() { CreationDate = clock.UtcNow(), SourceNode = node });
    }

    private void ClearFailed(Node node)
    {
      // Subscription failed so put node on failed list and log
      lock (failedSubscriptions)
      {
        failedSubscriptions.RemoveAll(f => f.Node.Id == node.Id);
      }
    }

    private void MarkAsFailed(Node node, string errorMessage)
    {
      // Subscription failed so put node on failed list and log
      lock (failedSubscriptions)
      {
        if (failedSubscriptions.Any(s => s.Node.Id == node.Id))
          return;
        failedSubscriptions.Add(new ZMQFailedSubscription(node, errorMessage, clock.UtcNow()));
      }
      eventBus.Publish(new ZMQFailedEvent() { CreationDate = clock.UtcNow(), SourceNode = node });
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
        subscriptions.TryAdd(address, new ZMQSubscription(nodeId, address, clock.UtcNow(), topic));
      }
    }

    private IRpcClient CreateZMQSubscriptionRpcClient(Node node)
    {
      return bitcoindFactory.Create(
        node.Host,
        node.Port,
        node.Username,
        node.Password,
        rpcResponseTimeoutSec,
        appSettings.RpcClient.MultiRequestTimeoutSec.Value,
        appSettings.RpcClient.RpcCallsOnStartupRetries.Value,
        appSettings.RpcClient.WaitBetweenRetriesMs.Value);
    }
  }

  class ZMQFailedSubscription
  {
    public ZMQFailedSubscription(Node node, string errorMessage, DateTime lastTryAt)
    {
      Node = node;
      LastTryAt = lastTryAt;
      LastError = errorMessage;
    }

    public Node Node { get; }

    public DateTime LastTryAt { get; set; }

    public String LastError { get; set; }

  }

  public class ZMQSubscription : IDisposable
  {
    private readonly List<string> topics = new();

    public ZMQSubscription(long nodeId, string address, DateTime lastPingAt, string topic = null)
    {
      NodeId = nodeId;
      Address = address;
      Socket = new SubscriberSocket();
      Socket.Connect(address);
      Socket.Options.ReconnectInterval = TimeSpan.FromMilliseconds(100);
      Socket.Options.ReconnectIntervalMax = TimeSpan.FromMilliseconds(0);
      LastPingAt = lastPingAt;
      LastMessageAt = null;

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

    public string[] Topics
    {
      get {
        return topics.ToArray();
      }
    }

    void IDisposable.Dispose()
    {
      Socket.Dispose();
    }

    public long NodeId { get; }

    public string Address { get; }

    public SubscriberSocket Socket { get; }

    public DateTime LastPingAt { get; set; }

    public DateTime? LastMessageAt { get; set; }

    public DateTime LastContactAt
    {
      get
      {
        if (LastMessageAt.HasValue && LastMessageAt.Value > LastPingAt)
          return LastMessageAt.Value;
        else
          return LastPingAt;
      }
    }
  }
}
