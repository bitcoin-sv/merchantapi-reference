// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain.Models;
using MerchantAPI.APIGateway.Rest.ViewModels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using MerchantAPI.APIGateway.Domain;

namespace MerchantAPI.APIGateway.Rest.Services
{
  public class ZMQStats : BackgroundService
  {

    readonly INodes nodes;
    readonly ZMQSubscriptionService subscriptionService;
    readonly ILogger<ZMQStats> logger;
    readonly int LOG_PERIOD_MIN;

    public ZMQStats(
      INodes nodes,
      ZMQSubscriptionService subscriptionService,
      ILogger<ZMQStats> logger,
      IOptions<AppSettings> appSettings
      )
    {
      this.nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
      this.subscriptionService = subscriptionService ?? throw new ArgumentNullException(nameof(subscriptionService));
      this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
      LOG_PERIOD_MIN = appSettings.Value.ZmqStatsLogPeriodMin.Value;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
      logger.LogInformation($"ZMQStats background service is starting");
      return base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
      logger.LogInformation($"ZMQStats background service is stopping");
      return base.StopAsync(cancellationToken);
    }

    private void LogZMQStats()
    {
      try
      {
        var result = nodes.GetNodes();
        var zmqStatuses = result.Select(n => (new ZmqStatusViewModelGet(n, subscriptionService.GetStatusForNode(n)).PrepareForLogging()));
        logger.LogInformation(
$@"** ZMQ Stats **
All active subscriptions: { subscriptionService.GetActiveSubscriptions().Count() }
Failed subscriptions: { subscriptionService.GetFailedSubscriptionsCount() }
ZMQ subscription status for { result.Count() } node(s): { string.Join(Environment.NewLine, zmqStatuses) }");
      }
      catch (Exception ex)
      {
        logger.LogError($"Exception in ZMQStats: { ex.Message }");
      }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
      while (!stoppingToken.IsCancellationRequested)
      {
        LogZMQStats();
        await Task.Delay(LOG_PERIOD_MIN * 60 * 1000, stoppingToken);
      }
    }
  }
}
