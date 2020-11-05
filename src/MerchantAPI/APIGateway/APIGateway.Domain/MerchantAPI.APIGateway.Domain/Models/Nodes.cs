// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.APIGateway.Domain.Repositories;
using MerchantAPI.Common;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MerchantAPI.Common.BitcoinRpc;
using MerchantAPI.APIGateway.Domain.Models.Events;
using MerchantAPI.Common.EventBus;
using System.Linq;
using MerchantAPI.Common.BitcoinRpc.Responses;
using MerchantAPI.Common.Clock;

namespace MerchantAPI.APIGateway.Domain.Models
{
  public class Nodes : INodes
  {
    readonly IRpcClientFactory bitcoindFactory;

    readonly INodeRepository nodeRepository;
    readonly IEventBus eventBus;
    readonly ILogger<Nodes> logger;
    readonly IClock clock;

    public Nodes(INodeRepository nodeRepository,
      IEventBus eventBus,
      IRpcClientFactory bitcoindFactory,
      ILogger<Nodes> logger,
      IClock clock
      )
    {
      this.bitcoindFactory = bitcoindFactory ?? throw new ArgumentNullException(nameof(bitcoindFactory));
      this.nodeRepository = nodeRepository ?? throw new ArgumentNullException(nameof(nodeRepository));
      this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
      this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
      this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<Node> CreateNodeAsync(Node node)
    {
      logger.LogInformation($"Adding node {node}");
      // Try to connect to node
      var bitcoind = bitcoindFactory.Create(node.Host, node.Port, node.Username, node.Password);
      try
      {
        // try to call some method to test if connectivity parameters are correct
        _ = await bitcoind.GetBlockCountAsync();
      }
      catch (Exception ex)
      {
        throw new BadRequestException($"The node was not added. Unable to connect to node {node.Host}:{node.Port}.", ex);
      }

      RpcActiveZmqNotification[] notifications;
      try
      {
        notifications = await bitcoind.ActiveZmqNotificationsAsync();
      }
      catch (Exception ex)
      {
        throw new BadRequestException($"Node at address '{node.Host}:{node.Port}' did not return a valid response to call 'activeZmqNotifications'", ex);
      }

      
      if (!notifications.Any() || notifications.Select(x => x.Notification).Intersect(ZMQTopic.RequiredZmqTopics).Count() != ZMQTopic.RequiredZmqTopics.Length)
      {
        var missingNotifications = ZMQTopic.RequiredZmqTopics.Except(notifications.Select(x => x.Notification));
        throw new BadRequestException($"Node '{node.Host}:{node.Port}', does not have all required zmq notifications enabled. Missing notifications ({string.Join(",", missingNotifications)})");
      }

      var createdNode = nodeRepository.CreateNode(node);

      eventBus.Publish(new NodeAddedEvent() { CreationDate = clock.UtcNow(), CreatedNode = createdNode });

      return createdNode;
    }

    public async Task<bool> UpdateNodeAsync(Node node)
    {
      logger.LogInformation($"Updating node {node}");
      // Try to connect to node
      var bitcoind = bitcoindFactory.Create(node.Host, node.Port, node.Username, node.Password);
      try
      {
        // try to call some method to test if connectivity parameters are correct
        _ = await bitcoind.GetBestBlockHashAsync();
      }
      catch (Exception ex)
      {
        throw new BadRequestException($"The node was not updated. Can not connect to node {node.Host}:{node.Port}.", ex);
      }

      return nodeRepository.UpdateNode(node);
    }

    public IEnumerable<Node> GetNodes()
    {
      return nodeRepository.GetNodes();
    }

    public Node GetNode(string id)
    {
      return nodeRepository.GetNode(id);
    }

    public int DeleteNode(string id)
    {
      logger.LogInformation($"Removing node id={id}");
      var node = nodeRepository.GetNode(id);
      if (node != null)
      {
        eventBus.Publish(new NodeDeletedEvent() { CreationDate = clock.UtcNow(), DeletedNode = node });
      }
      return nodeRepository.DeleteNode(id);
    }
  }
}
