// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.APIGateway.Domain;
using MerchantAPI.APIGateway.Domain.Actions;
using MerchantAPI.APIGateway.Domain.Models.Events;
using MerchantAPI.Common.EventBus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Rest.Services
{
  public class NotificationService : BackgroundServiceWithSubscriptions<NotificationService>
  {
    readonly INotificationAction notificationAction;
    readonly IOptionsMonitor<AppSettings> options;
    EventBusSubscription<NewNotificationEvent> newNotificationEventSubscription;

    public NotificationService(INotificationAction notificationAction, IOptionsMonitor<AppSettings> options, ILogger<NotificationService> logger, IEventBus eventBus) : base(logger, eventBus)
    {
      this.notificationAction = notificationAction ?? throw new ArgumentNullException(nameof(notificationAction));
      this.options = options ?? throw new ArgumentNullException(nameof(options));
    }


    protected override async Task ExecuteActualWorkAsync(CancellationToken stoppingToken)
    {
      while (!stoppingToken.IsCancellationRequested)
      {
        notificationAction.ProcessAndSendNotifications();
        await Task.Delay(options.CurrentValue.NotificationIntervalSec * 1000);
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
      await notificationAction.SendNotificationFromEventAsync(e);
    }

  }
}
