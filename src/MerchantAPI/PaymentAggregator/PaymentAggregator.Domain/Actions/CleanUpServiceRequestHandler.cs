// Copyright (c) 2020 Bitcoin Association

using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading;
using System.Threading.Tasks;
using MerchantAPI.PaymentAggregator.Domain.Repositories;
using Microsoft.Extensions.Hosting;

namespace MerchantAPI.PaymentAggregator.Domain.Actions
{
  public class CleanUpServiceRequestHandler : BackgroundService
  {
    readonly IServiceRequestRepository serviceRequestRepository;
    protected readonly ILogger<CleanUpServiceRequestHandler> logger;
    protected readonly int cleanUpServiceRequestPeriodSec;
    readonly int cleanUpServiceRequestAfterDays;


    public CleanUpServiceRequestHandler(IServiceRequestRepository serviceRequestRepository, ILogger<CleanUpServiceRequestHandler> logger, IOptions<AppSettings> options)
    {
      this.serviceRequestRepository = serviceRequestRepository ?? throw new ArgumentNullException(nameof(serviceRequestRepository));
      this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
      cleanUpServiceRequestPeriodSec = options.Value.CleanUpServiceRequestPeriodSec;
      cleanUpServiceRequestAfterDays = options.Value.CleanUpServiceRequestAfterDays;
    }


    public override Task StartAsync(CancellationToken cancellationToken)
    {
      logger.LogInformation($"CleanUpServiceRequestHandler background service is starting");
      return base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
      logger.LogInformation($"CleanUpServiceRequestHandler background service is stopping");
      return base.StopAsync(cancellationToken);
    }

    protected async Task CleanUpServiceRequestAsync(DateTime now)
    {
      try
      {
        await serviceRequestRepository.CleanUpServiceRequestAsync(now.AddDays(-cleanUpServiceRequestAfterDays));
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
        await CleanUpServiceRequestAsync(DateTime.UtcNow);
        await Task.Delay(cleanUpServiceRequestPeriodSec * 1000, stoppingToken);
      }
    }
  }
}
