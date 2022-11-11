// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain.Models;
using MerchantAPI.APIGateway.Rest.ViewModels.APIStatus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using MerchantAPI.APIGateway.Domain;
using MerchantAPI.APIGateway.Domain.Actions;

namespace MerchantAPI.APIGateway.Rest.Services
{
  public class APIStatusService : BackgroundService
  {

    readonly INodes nodes;
    readonly IBlockParser blockParser;
    readonly IMapi mapi;
    readonly AppSettings appSettings;
    readonly ZMQSubscriptionService subscriptionService;
    readonly ILogger<APIStatusService> logger;
    readonly int LOG_PERIOD_MIN;

    public APIStatusService(
      INodes nodes,
      IBlockParser blockParser,
      IMapi mapi,
      IOptions<AppSettings> options,
      ZMQSubscriptionService subscriptionService,
      ILogger<APIStatusService> logger
      )
    {
      this.nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
      this.blockParser = blockParser ?? throw new ArgumentNullException(nameof(blockParser));
      this.mapi = mapi ?? throw new ArgumentNullException(nameof(mapi));
      appSettings = options.Value;
      this.subscriptionService = subscriptionService ?? throw new ArgumentNullException(nameof(subscriptionService));
      this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
      LOG_PERIOD_MIN = appSettings.Zmq.StatsLogPeriodMin.Value;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
      logger.LogInformation($"{nameof(APIStatusService)} is starting.");
      return base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
      logger.LogInformation($"{nameof(APIStatusService)} is stopping.");
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
        logger.LogError($"Exception in LogZMQStats: { ex.Message }");
      }
    }

    private void LogBlockParserStats()
    {
      try
      {
        var status = blockParser.GetBlockParserStatus();
        logger.LogInformation(
$@"** BlockParser Stats **
{(new BlockParserStatusViewModelGet(status,
          appSettings.DontParseBlocks.Value,
          appSettings.DontInsertTransactions.Value,
          appSettings.DeltaBlockHeightForDoubleSpendCheck.Value,
          appSettings.MaxBlockChainLengthForFork.Value)).PrepareForLogging()}");
      }
      catch (Exception ex)
      {
        logger.LogError($"Exception in LogBlockParserStats: { ex.Message }");
      }
    }

    private void LogSubmitTxMapiStats()
    {
      try
      {
        var status = mapi.GetSubmitTxStatus();
        logger.LogInformation(
$@"** Submit tx mAPI Stats **
{status.PrepareForLogging()}");
      }
      catch (Exception ex)
      {
        logger.LogError($"Exception in LogSubmitTxMapiStats: {ex.Message}");
      }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
      while (!stoppingToken.IsCancellationRequested)
      {
        LogZMQStats();
        LogBlockParserStats();
        LogSubmitTxMapiStats();
        await Task.Delay(LOG_PERIOD_MIN * 60 * 1000, stoppingToken);
      }
    }
  }
}
