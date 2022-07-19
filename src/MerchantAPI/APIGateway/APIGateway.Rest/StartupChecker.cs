// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain.Models;
using MerchantAPI.APIGateway.Domain.Repositories;
using MerchantAPI.Common.BitcoinRpc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MerchantAPI.APIGateway.Domain.Actions;
using MerchantAPI.APIGateway.Domain;
using MerchantAPI.APIGateway.Domain.NotificationsHandler;
using MerchantAPI.Common.Startup;
using MerchantAPI.Common.Tasks;
using MerchantAPI.APIGateway.Rest.Database;
using MerchantAPI.Common.BitcoinRpc.Responses;
using Microsoft.Extensions.Options;

namespace MerchantAPI.APIGateway.Rest
{
  public class StartupChecker: IStartupChecker 
  {
    readonly INodeRepository nodeRepository;
    readonly INodes nodes;
    readonly ILogger<StartupChecker> logger;
    readonly IRpcClientFactory rpcClientFactory;
    readonly IList<Node> accessibleNodes = new List<Node>();
    readonly IBlockParser blockParser;
    readonly INotificationsHandler notificationsHandler;
    readonly IMinerId minerId;
    readonly IDbManager dbManager;
    readonly AppSettings appSettings;
    bool nodesAccessible;

    public StartupChecker(INodeRepository nodeRepository,
                          INodes nodes,
                          IRpcClientFactory rpcClientFactory,
                          IMinerId minerId,
                          IBlockParser blockParser,
                          IDbManager dbManager,
                          INotificationsHandler notificationsHandler,
                          ILogger<StartupChecker> logger,
                          IOptions<AppSettings> options)
    {
      this.rpcClientFactory = rpcClientFactory ?? throw new ArgumentNullException(nameof(rpcClientFactory));
      this.nodeRepository = nodeRepository ?? throw new ArgumentNullException(nameof(nodeRepository));
      this.nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
      this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
      this.blockParser = blockParser ?? throw new ArgumentNullException(nameof(blockParser));
      this.dbManager = dbManager ?? throw new ArgumentNullException(nameof(dbManager));
      this.minerId = minerId ?? throw new ArgumentNullException(nameof(nodeRepository));
      this.notificationsHandler = notificationsHandler ?? throw new ArgumentNullException(nameof(notificationsHandler));
      appSettings = options.Value;
    }

    public async Task<bool> CheckAsync(bool testingEnvironment)
    {
      logger.LogInformation("Health checks starting.");
      try
      {
        logger.LogInformation($"API version: {Const.MERCHANT_API_VERSION}");
        logger.LogInformation($"Build version: {Const.MERCHANT_API_BUILD_VERSION}");

        RetryUtils.ExecAsync(() => TestDBConnection(), retry: appSettings.DbConnection.StartupTestConnectionMaxRetries.Value, errorMessage: "Unable to open connection to database").Wait();
        ExecuteCreateDb();
        if (!testingEnvironment)
        {
          await TestNodesConnectivityAndVersionAsync();
          await CheckNodesZmqNotificationsAsync();
          await TestMinerId();
          await CheckBlocksAsync();
          await MarkUncompleteNotificationsAsFailedAsync();
        }
        logger.LogInformation("Health checks completed successfully.");
      }
      catch (Exception ex)
      {
        logger.LogError("Health checks failed. {0}", ex.GetBaseException().ToString());
        // If exception was thrown then we stop the application. All methods in try section must pass without exception
        if (testingEnvironment)
        {
          throw;
        }
        return false;
      }
      
      return true;
    }


    private Task TestDBConnection()
    {
      if (dbManager.DatabaseExists())
      {
        logger.LogInformation($"Successfully connected to DB.");
      }
      return Task.CompletedTask;
    }

    private async Task TestMinerId()
    {
      try
      {
        logger.LogInformation($"Checking MinerId");
        var currentMinerId = await minerId.GetCurrentMinerIdAsync();
        await minerId.SignWithMinerIdAsync(currentMinerId, "5bdc7d2ca32915a311f91a6b4b8dcefd746b1a73d355a65cbdee425e4134d682");
        logger.LogInformation($"MinerId check completed successfully");
      }
      catch (Exception e)
      {
        logger.LogError($"Can not access MinerID. {e.Message}");
        throw;
      }
    }

    private void ExecuteCreateDb()
    {
      logger.LogInformation($"Starting with execution of CreateDb ...");


      if (dbManager.CreateDb(out string errorMessage, out string errorMessageShort))
      {
        logger.LogInformation("CreateDB finished successfully.");
      }
      else
      {
        // if error we must stop application
        throw new Exception($"Error when executing CreateDB: { errorMessage }{ Environment.NewLine }ErrorMessage: {errorMessageShort}");
      }

      logger.LogInformation($"ExecuteCreateDb completed.");
    }

    private async Task TestNodesConnectivityAndVersionAsync()
    {
      logger.LogInformation($"Checking nodes connectivity");

      var nodes = nodeRepository.GetNodes();
      if (!nodes.Any())
      {
        logger.LogWarning("There are no nodes present in database.");
      }

      foreach (var node in nodes)
      {
        var rpcClient = CreateStartupRpcClient(node);
        try
        {
          var (valid, error, warnings) = await Nodes.IsNodeValidAsync(rpcClient, appSettings);
          if (!valid)
          {
            logger.LogError(error);
          }
          if (warnings.Any())
          {
            logger.LogWarning($"Validation of node {node} returned warnings: {string.Join(Environment.NewLine, warnings)}");
          }
          accessibleNodes.Add(node);
          nodesAccessible = true;
        }
        catch (Exception)
        {
          logger.LogWarning($"Node at address '{node.Host}:{node.Port}' is unreachable");
        }
      }
      logger.LogInformation($"Nodes connectivity check complete");
    }

    private IRpcClient CreateStartupRpcClient(Node node)
    {
      return rpcClientFactory.Create(
        node.Host,
        node.Port,
        node.Username,
        node.Password,
        appSettings.RpcClient.RequestTimeoutSec.Value,
        appSettings.RpcClient.MultiRequestTimeoutSec.Value,
        appSettings.RpcClient.RpcCallsOnStartupRetries.Value,
        appSettings.RpcClient.WaitBetweenRetriesMs.Value);
    }

    private async Task CheckNodesZmqNotificationsAsync()
    {
      logger.LogInformation($"Checking nodes zmq notification services");
      foreach (var node in accessibleNodes)
      {
        var rpcClient = CreateStartupRpcClient(node);
        RpcActiveZmqNotification[] notifications = null;
        try
        {
          notifications = await rpcClient.ActiveZmqNotificationsAsync(retry: true);
          
          if (!notifications.Any() || notifications.Select(x => x.Notification).Intersect(ZMQTopic.RequiredZmqTopics).Count() != ZMQTopic.RequiredZmqTopics.Length)
          {
            var missingNotifications = ZMQTopic.RequiredZmqTopics.Except(notifications.Select(x => x.Notification));
            logger.LogError($"Node '{node.Host}:{node.Port}', does not have all required zmq notifications enabled. Missing notifications ({string.Join(",", missingNotifications)})");
          }
        }
        catch (Exception ex)
        {
          logger.LogError($"Node at address '{node.Host}:{node.Port}' did not return a valid response to call 'activeZmqNotifications'", ex);
        }

        if (!nodes.IsZMQNotificationsEndpointValid(node, notifications, out string error))
        {
          logger.LogWarning(error);
        }
      }
      logger.LogInformation($"Nodes zmq notification services check complete");
    }

    private async Task CheckBlocksAsync()
    {
      if (nodesAccessible)
      {
        await blockParser.InitializeDBAsync();
      }
    }

    private async Task MarkUncompleteNotificationsAsFailedAsync()
    {
      try
      {
        await notificationsHandler.MarkUncompleteNotificationsAsFailedAsync();
        logger.LogInformation("Successfully marked notifications that were not instantly sent to be sent from background job.");
      }
      catch(Exception ex)
      {
        logger.LogError($"Error while trying to mark all unprocessed notifications for slow queue. Error:{ex.GetBaseException().Message}");
      }
    }
  }
}
