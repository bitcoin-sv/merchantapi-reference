// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain;
using MerchantAPI.APIGateway.Domain.Actions;
using MerchantAPI.APIGateway.Domain.Repositories;
using MerchantAPI.Common.Clock;
using MerchantAPI.Common.EventBus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Test.Functional.CleanUpTx
{
  public class CleanUpTxWithPauseHandlerForTest : CleanUpTxHandler
  {
    bool paused = false;
    readonly IEventBus eventBus;
    readonly IClock clock;

    public CleanUpTxWithPauseHandlerForTest(IEventBus eventBus, ITxRepository txRepository, ILogger<CleanUpTxHandler> logger, IOptions<AppSettings> options, IClock clock)
      : base(txRepository, logger, options)
    {
      this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
      this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
  }

    public Task ResumeAsync(CancellationToken cancellationToken)
    {
      logger.LogInformation($"CleanUpTxHandler background service is resuming");
      paused = false;
      return StartAsync(cancellationToken);
    }

    public void Pause()
    {
      logger.LogInformation($"CleanUpTxHandler background service is pausing");
      paused = true;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
      while (!stoppingToken.IsCancellationRequested)
      {
        if (!paused)
        {
          await CleanUpTxAsync(clock.UtcNow());
          eventBus.Publish(new CleanUpTxTriggeredEvent { CleanUpTriggeredAt = clock.UtcNow() });
        }
        await Task.Delay(cleanUpTxPeriodSec * 1000, stoppingToken);
      }
    }

  }
}
