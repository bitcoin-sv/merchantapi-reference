// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.APIGateway.Domain.Models;
using MerchantAPI.APIGateway.Domain.Repositories;
using MerchantAPI.Common;
using MerchantAPI.Common.BitcoinRpc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MerchantAPI.APIGateway.Domain.Actions;
using Microsoft.Extensions.Configuration;
using MerchantAPI.APIGateway.Domain;

namespace MerchantAPI.APIGateway.Rest
{
  public class StartupChecker : IHostedService
  {
    readonly INodeRepository nodeRepository;
    readonly IHostApplicationLifetime hostApplicationLifetime;
    readonly ILogger<StartupChecker> logger;
    readonly IRpcClientFactory rpcClientFactory;
    readonly IList<Node> accessibleNodes = new List<Node>();
    readonly IBlockParser blockParser;
    private readonly IMinerId minerId;
    bool nodesAccessible;
    readonly IConfiguration configuration;

    public StartupChecker(INodeRepository nodeRepository,
                          IRpcClientFactory rpcClientFactory,
                          IHostApplicationLifetime hostApplicationLifetime,
                          IMinerId minerId,
                          IBlockParser blockParser,
                          ILogger<StartupChecker> logger,
                          IConfiguration configuration)
    {
      this.rpcClientFactory = rpcClientFactory ?? throw new ArgumentNullException(nameof(rpcClientFactory));
      this.nodeRepository = nodeRepository ?? throw new ArgumentNullException(nameof(nodeRepository));
      this.hostApplicationLifetime = hostApplicationLifetime;
      this.logger = logger ?? throw new ArgumentException(nameof(logger));
      this.blockParser = blockParser ?? throw new ArgumentException(nameof(blockParser));
      this.minerId = minerId ?? throw new ArgumentException(nameof(nodeRepository));
      this.configuration = configuration ?? throw new ArgumentException(nameof(configuration));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
      logger.LogInformation("Health checks starting.");
      try
      {
        RetryUtils.ExecAsync(() => TestDBConnection(), retry: 10, errorMessage: "Unable to open connection to database").Wait();
        TestNodesConnectivityAsync().Wait();
        CheckNodesZmqNotificationsAsync().Wait();
        TestMinerId().Wait();
        CheckBlocksAsync().Wait();
        logger.LogInformation("Health checks completed successfully.");
      }
      catch (Exception ex)
      {
        logger.LogError("Health checks failed. {0}", ex.GetBaseException().ToString());
        // If exception was thrown then we stop the application. All methods in try section must pass without exception
        hostApplicationLifetime.StopApplication();
      }
      
      return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
      return Task.CompletedTask;
    }

    private Task TestDBConnection()
    {
      logger.LogInformation($"Trying to connect to DB: '{configuration["ConnectionStrings:DBConnectionString"]}'");
      var nodes = nodeRepository.GetNodes();
      if (!nodes.Any())
      {
        logger.LogWarning("There are no nodes present in database.");
      }

      logger.LogInformation($"Successfully connected to DB.");
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

    private async Task TestNodesConnectivityAsync()
    {
      logger.LogInformation($"Checking nodes connectivity");

      var nodes = nodeRepository.GetNodes();
      foreach (var node in nodes)
      {
        var rpcClient = rpcClientFactory.Create(node.Host, node.Port, node.Username, node.Password);
        rpcClient.RequestTimeout = TimeSpan.FromSeconds(3);
        rpcClient.NumOfRetries = 10;
        try
        {
          await rpcClient.GetBlockCountAsync();
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

    private async Task CheckNodesZmqNotificationsAsync()
    {
      logger.LogInformation($"Checking nodes zmq notification services");
      foreach (var node in accessibleNodes)
      {
        var rpcClient = rpcClientFactory.Create(node.Host, node.Port, node.Username, node.Password);
        try
        {
          var notifications = await rpcClient.ActiveZmqNotificationsAsync();
          
          if (!notifications.Any() || notifications.Select(x => x.Notification).Intersect(Const.RequiredZmqNotifications).Count() != Const.RequiredZmqNotifications.Length)
          {
            var missingNotifications = Const.RequiredZmqNotifications.Except(notifications.Select(x => x.Notification));
            logger.LogError($"Node '{node.Host}:{node.Port}', does not have all required zmq notifications enabled. Missing notifications ({string.Join(",", missingNotifications)})");
          }
        }
        catch (Exception ex)
        {
          logger.LogError($"Node at address '{node.Host}:{node.Port}' did not return a valid response to call 'activeZmqNotifications'", ex);
        }
      }
      logger.LogInformation($"Nodes zmq notification services check complete");
    }

    private async Task CheckBlocksAsync()
    {
      if (nodesAccessible)
      {
        await blockParser.InitializeDB();
      }
    }
  }
}
