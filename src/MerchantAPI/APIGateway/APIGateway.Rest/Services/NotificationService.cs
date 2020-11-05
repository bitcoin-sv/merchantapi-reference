// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.APIGateway.Domain;
using MerchantAPI.APIGateway.Domain.Models.Events;
using MerchantAPI.APIGateway.Domain.NotificationsHandler;
using MerchantAPI.APIGateway.Domain.Repositories;
using MerchantAPI.Common.EventBus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Rest.Services
{

  /// <summary>
  /// Default HttpClient factory used when performing callbacks in production code.
  /// </summary>
  public class NotificationServiceHttpClientFactoryDefault : INotificationServiceHttpClientFactory
  {
    public const string ClientName = "Notification.Service.Http.Client";
    IHttpClientFactory factory;
    public NotificationServiceHttpClientFactoryDefault(IHttpClientFactory defaultFactory)
    {
      this.factory = defaultFactory ?? throw new ArgumentNullException(nameof(defaultFactory));
      
    }

    public HttpClient CreateClient(string clientName)
    {
      return factory.CreateClient(clientName);
    }
  }


  public class NotificationService : BackgroundServiceWithSubscriptions<NotificationService>
  {
    const int NoOfRecordsBatch = 100;
    int skipRecords = 0;
    readonly INotificationsHandler notificationsHandler;
    readonly ITxRepository txRepository;
    readonly Notification notificationSettings;
    EventBusSubscription<NewNotificationEvent> newNotificationEventSubscription;

    public NotificationService(IOptionsMonitor<AppSettings> options, ILogger<NotificationService> logger, 
                               IEventBus eventBus, INotificationsHandler notificationsHandler, ITxRepository txRepository) : base(logger, eventBus)
    {
      this.notificationsHandler = notificationsHandler ?? throw new ArgumentNullException(nameof(notificationsHandler));
      this.txRepository = txRepository ?? throw new ArgumentNullException(nameof(txRepository));
      notificationSettings = options.CurrentValue.Notification;
    }


    protected override async Task ExecuteActualWorkAsync(CancellationToken stoppingToken)
    {
      while (!stoppingToken.IsCancellationRequested)
      {
        await PrepareAndSendNotificationsAsync(stoppingToken);
        await Task.Delay(notificationSettings.NotificationIntervalSec * 1000, stoppingToken);
      }
    }

    protected override void UnsubscribeFromEventBus()
    {
      eventBus?.TryUnsubscribe(newNotificationEventSubscription);
      newNotificationEventSubscription = null;
    }

    protected override void SubscribeToEventBus(CancellationToken stoppingToken)
    {
      newNotificationEventSubscription = eventBus.Subscribe<NewNotificationEvent>();
      _ = newNotificationEventSubscription.ProcessEventsAsync(stoppingToken, logger, ProcessNotificationAsync);
    }

    protected override Task ProcessMissedEvents()
    {
      return Task.CompletedTask;
    }

    private async Task ProcessNotificationAsync(NewNotificationEvent e)
    {
      await notificationsHandler.EnqueueNotificationAsync(e);
    }

    private async Task PrepareAndSendNotificationsAsync(CancellationToken stoppingToken)
    {
      try
      {
        var waitingNotifications = await txRepository.GetNotificationsWithErrorAsync(notificationSettings.NotificationsRetryCount, skipRecords, NoOfRecordsBatch);
        int numOfNotifications = waitingNotifications.Count;
        
        // We reached the end of failed notifications...let's start from the beginning again
        if (numOfNotifications == 0 && skipRecords > 0)
        {
          skipRecords = 0;
          waitingNotifications = await txRepository.GetNotificationsWithErrorAsync(notificationSettings.NotificationsRetryCount, skipRecords, NoOfRecordsBatch);
          numOfNotifications = waitingNotifications.Count;
        }
        if (numOfNotifications > 0)
        {
          logger.LogInformation($"Processing batch of {numOfNotifications}' notifications.");
        }

        int successfull = 0;
        foreach (var notificationData in waitingNotifications)
        {
          if (stoppingToken.IsCancellationRequested) break;

          using var client = notificationsHandler.GetClient(notificationData.CallbackUrl);
          if (await notificationsHandler.ProcessNotificationAsync(client, notificationData, notificationSettings.SlowHostResponseTimeoutMS, stoppingToken)) successfull++;
        }
        skipRecords += numOfNotifications - successfull;
      }
      catch (Exception ex)
      {
        logger.LogError($"Error processing failed notifications from database. Error: {ex.GetBaseException().Message}");
      }
    }
  }
}
