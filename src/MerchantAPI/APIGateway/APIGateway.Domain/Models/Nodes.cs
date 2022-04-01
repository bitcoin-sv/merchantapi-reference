// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain.Models.Events;
using MerchantAPI.APIGateway.Domain.Repositories;
using MerchantAPI.Common.BitcoinRpc;
using MerchantAPI.Common.BitcoinRpc.Responses;
using MerchantAPI.Common.Clock;
using MerchantAPI.Common.EventBus;
using MerchantAPI.Common.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Domain.Models
{
  public class Nodes : INodes
  {
    readonly IRpcClientFactory bitcoindFactory;

    readonly INodeRepository nodeRepository;
    readonly IEventBus eventBus;
    readonly ILogger<Nodes> logger;
    readonly IClock clock;
    readonly IZMQEndpointChecker ZMQEndpointChecker;
    readonly RpcClientSettings rpcClientSettings;

    public Nodes(INodeRepository nodeRepository,
      IEventBus eventBus,
      IRpcClientFactory bitcoindFactory,
      ILogger<Nodes> logger,
      IClock clock,
      IZMQEndpointChecker ZMQEndpointChecker,
      IOptions<AppSettings> options
      )
    {
      this.bitcoindFactory = bitcoindFactory ?? throw new ArgumentNullException(nameof(bitcoindFactory));
      this.nodeRepository = nodeRepository ?? throw new ArgumentNullException(nameof(nodeRepository));
      this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
      this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
      this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
      this.ZMQEndpointChecker = ZMQEndpointChecker ?? throw new ArgumentNullException(nameof(ZMQEndpointChecker));
      rpcClientSettings = options.Value.RpcClient;
    }

    private async Task ValidateNode(Node node, bool isUpdate = false)
    {
      // Try to connect to node
      var bitcoind = bitcoindFactory.Create(
        node.Host,
        node.Port,
        node.Username,
        node.Password,
        rpcClientSettings.RequestTimeoutSec.Value,
        rpcClientSettings.MultiRequestTimeoutSec.Value,
        rpcClientSettings.NumOfRetries.Value,
        rpcClientSettings.WaitBetweenRetriesMs.Value);
      try
      {
        // try to call some method to test if connectivity parameters are correct
        var networkInfo = await bitcoind.GetNetworkInfoAsync(retry: true);

        if (!Nodes.IsNodeVersionValid(networkInfo.Version, out string versionError))
        {
          throw new BadRequestException(versionError);
        }
      }
      catch (Exception ex)
      {
        throw new BadRequestException($"The node was not { (isUpdate ? "updated" : "added") }. Unable to connect to node {node.Host}:{node.Port}.", ex);
      }

      RpcActiveZmqNotification[] notifications;
      try
      {
        notifications = await bitcoind.ActiveZmqNotificationsAsync(retry: true);
      }
      catch (Exception ex)
      {
        throw new BadRequestException($"Node at address '{node.Host}:{node.Port}' did not return a valid response to call 'activeZmqNotifications'", ex);
      }
      
      if (!IsZMQNotificationsEndpointValid(node, notifications, out string error))
      {
        throw new BadRequestException(error);
      }

      if (!notifications.Any() || notifications.Select(x => x.Notification).Intersect(ZMQTopic.RequiredZmqTopics).Count() != ZMQTopic.RequiredZmqTopics.Length)
      {
        var missingNotifications = ZMQTopic.RequiredZmqTopics.Except(notifications.Select(x => x.Notification));
        throw new BadRequestException($"Node '{node.Host}:{node.Port}', does not have all required zmq notifications enabled. Missing notifications ({string.Join(",", missingNotifications)})");
      }

    }

    public async Task<Node> CreateNodeAsync(Node node)
    {
      logger.LogInformation($"Adding node {node}");

      await ValidateNode(node);

      var createdNode = nodeRepository.CreateNode(node);

      eventBus.Publish(new NodeAddedEvent() { CreationDate = clock.UtcNow(), CreatedNode = createdNode });

      return createdNode;
    }

    public async Task<bool> UpdateNodeAsync(Node node)
    {
      logger.LogInformation($"Updating node {node}");

      await ValidateNode(node, isUpdate: true);

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

    public static bool IsNodeVersionValid(long version, out string error)
    {
      error = null;

      string requiredNodeVersion = Const.MinBitcoindRequired();
      if (!string.IsNullOrEmpty(requiredNodeVersion))
      {
        long clientVersion = GetBitcoindClientVersion(requiredNodeVersion);
        if (version < clientVersion)
        {
          error = $"Node version must be at least { requiredNodeVersion }.";
        }
      }
      return error == null;
    }

    public bool IsZMQNotificationsEndpointValid(Node node, RpcActiveZmqNotification[] notifications, out string error)
    {
      error = null;

      if (!string.IsNullOrEmpty(node.ZMQNotificationsEndpoint))
      {
        // check if ZMQNotificationsEndpoint exists on this or another node.
        if (nodeRepository.ZMQNotificationsEndpointExists(node.ToExternalId(), node.ZMQNotificationsEndpoint))
        {
          error = $"The value {node.ZMQNotificationsEndpoint} of {nameof(node.ZMQNotificationsEndpoint)} field already exists on another node.";
        }
        else if (!ZMQEndpointChecker.IsZMQNotificationsEndpointReachable(node.ZMQNotificationsEndpoint))
        {
          error = $"ZMQNotificationsEndpoint: '{node.ZMQNotificationsEndpoint}' is unreachable.";
        }
      }
      else if (notifications != null)
      {
        foreach (var n in notifications.GroupBy(x => x.Address, x => x.Notification, (key, values) => new { Address = key, Notifications = values.ToList() }).ToList())
        {
          if (!ZMQEndpointChecker.IsZMQNotificationsEndpointReachable(n.Address))
          {
            if (!string.IsNullOrEmpty(error))
            {
              error += Environment.NewLine;
            }
            error += $"Node's ZMQNotification for {String.Join(", ", n.Notifications)}: '{n.Address}' is unreachable.";
          }
        }
      }
      return error == null;
    }

    static long GetBitcoindClientVersion(int clientVersionMajor, int clientVersionMinor, int clientVersionRevision)
    {
      // Initial bitcoin has its own way of calculating the client version number
      // i.e CLIENT_VERSION. Bitcoin SV start at a very low version numbers.
      // In order to keep backward compatibility, the calculated CLIENT_VERSION
      // is shifted in the way the lowest version of Bitcoin SV is still higher 
      // than the highest calculated version in the traditional Bitcoin.

      int clientVersionBuild = 0; // currently not important for mAPI 
      const int svVersionShift = 100000000;
      int clientVersion = svVersionShift +
                          1000000 * clientVersionMajor +
                          10000 * clientVersionMinor +
                          100 * clientVersionRevision +
                          1 * clientVersionBuild;
      return clientVersion;
    }

    static long GetBitcoindClientVersion(string requiredNodeVersion)
    {
      var values = (requiredNodeVersion.Split(".")).Select(x => int.Parse(x)).ToArray();
      (int clientVersionMajor, int clientVersionMinor, int clientVersionRevision) = (values[0], values[1], values[2]);
      long clientVersion = GetBitcoindClientVersion(clientVersionMajor, clientVersionMinor, clientVersionRevision);
      return clientVersion;
    }

  }
}
