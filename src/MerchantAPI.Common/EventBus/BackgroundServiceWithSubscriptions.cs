// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MerchantAPI.Common.EventBus
{
  public abstract class BackgroundServiceWithSubscriptions<TDerived> : BackgroundService
  {
    protected readonly ILogger<TDerived> logger;
    protected readonly IEventBus eventBus;

    protected BackgroundServiceWithSubscriptions(ILogger<TDerived> logger, IEventBus eventBus)
    {
      this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
      this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
      logger.LogInformation($"{typeof(TDerived)} background service is starting");
      return base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken stoppingToken)
    {
      logger.LogInformation($"{typeof(TDerived)} background service is stopping");
      return base.StopAsync(stoppingToken);
    }

    protected abstract void UnsubscribeFromEventBus();

    protected abstract void SubscribeToEventBus(CancellationToken stoppingToken);

    protected abstract Task ProcessMissedEvents();

    public override void Dispose()
    {
      UnsubscribeFromEventBus();
      base.Dispose();
    }

    protected virtual async Task ExecuteActualWorkAsync(CancellationToken stoppingToken)
    {
      // By default, just wait until stop is requested
      await Task.Delay(TimeSpan.FromMilliseconds(-1), stoppingToken);
    }

    // This method is sealed override ExecuteActualWorkAsync to do perform actual work
    protected sealed override async Task ExecuteAsync(CancellationToken stoppingToken) 
    {
      await ProcessMissedEvents();
      SubscribeToEventBus(stoppingToken);

      try
      {
        await ExecuteActualWorkAsync(stoppingToken);
      }
      catch (TaskCanceledException)
      {
      }

      UnsubscribeFromEventBus();
    }

  }
}
