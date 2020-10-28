// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.APIGateway.Domain.Models;
using MerchantAPI.APIGateway.Domain.Models.Events;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Domain.NotificationsHandler
{
  public interface INotificationsHandler
  {
    Task EnqueueNotificationAsync(NewNotificationEvent notificationEvent);

    Task<bool> ProcessNotificationAsync(HttpClient client, NotificationData notification, int requestTimeout, CancellationToken stoppingToken);

    HttpClient GetClient(string callbackUrl);

    Task MarkUncompleteNotificationsAsFailedAsync();
  }
}
