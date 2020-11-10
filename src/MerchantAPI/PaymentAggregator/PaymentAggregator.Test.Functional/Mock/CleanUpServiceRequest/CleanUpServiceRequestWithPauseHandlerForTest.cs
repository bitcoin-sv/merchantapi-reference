// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.Common.Clock;
using MerchantAPI.Common.EventBus;
using MerchantAPI.PaymentAggregator.Domain;
using MerchantAPI.PaymentAggregator.Domain.Actions;
using MerchantAPI.PaymentAggregator.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MerchantAPI.PaymentAggregator.Test.Functional.Mock.CleanUpServiceRequest
{
  public class CleanUpServiceRequestWithPauseHandlerForTest : CleanUpServiceRequestHandler
  {
    bool paused = false;
    readonly IEventBus eventBus;
    readonly IClock clock;
    public CleanUpServiceRequestWithPauseHandlerForTest(IEventBus eventBus, IClock clock, IServiceRequestRepository serviceRequestRepository, ILogger<CleanUpServiceRequestWithPauseHandlerForTest> logger, IOptions<AppSettings> options)
       : base(serviceRequestRepository, logger, options)
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
          await CleanUpServiceRequestAsync(clock.UtcNow());
          eventBus.Publish(new CleanUpServiceRequestTriggeredEvent { CleanUpTriggeredAt = clock.UtcNow() });
        }
        await Task.Delay(cleanUpServiceRequestPeriodSec * 1000, stoppingToken);
      }
    }
  }
}
