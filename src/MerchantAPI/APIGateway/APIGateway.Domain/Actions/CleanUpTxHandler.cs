// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain.Repositories;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Domain.Actions
{
  public class CleanUpTxHandler : BackgroundService
  {

    readonly ITxRepository txRepository;
    protected readonly ILogger<CleanUpTxHandler> logger;
    protected readonly int cleanUpTxPeriodSec;
    readonly int cleanUpTxAfterDays;


    public CleanUpTxHandler(ITxRepository txRepository, ILogger<CleanUpTxHandler> logger, IOptions<AppSettings> options)
    {
      this.txRepository = txRepository ?? throw new ArgumentNullException(nameof(txRepository));
      this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
      cleanUpTxPeriodSec = options.Value.CleanUpTxPeriodSec.Value;
      cleanUpTxAfterDays = options.Value.CleanUpTxAfterDays.Value;
    }


    public override Task StartAsync(CancellationToken cancellationToken)
    {
      logger.LogInformation($"CleanUpTxHandler background service is starting");
      return base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
      logger.LogInformation($"CleanUpTxHandler background service is stopping");
      return base.StopAsync(cancellationToken);
    }

    protected async Task CleanUpTxAsync(DateTime now)
    {
      try
      {
        var (blocks, txs) = await txRepository.CleanUpTxAsync(now.AddDays(-cleanUpTxAfterDays));
        logger.LogInformation($"CleanUpTxHandler: deleted {blocks} blocks and {txs} txs.");
      }
      catch (Exception ex)
      {
        logger.LogError($"Exception in CleanupHandler: { ex.Message }");
      }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
      while (!stoppingToken.IsCancellationRequested)
      {
        await CleanUpTxAsync(DateTime.UtcNow);
        await Task.Delay(cleanUpTxPeriodSec * 1000, stoppingToken);
      }
    }
  }
}
