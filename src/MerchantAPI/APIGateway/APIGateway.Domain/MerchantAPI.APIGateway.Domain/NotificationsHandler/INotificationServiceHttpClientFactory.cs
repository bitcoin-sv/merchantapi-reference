// Copyright (c) 2020 Bitcoin Association

using System.Net.Http;

namespace MerchantAPI.APIGateway.Domain.NotificationsHandler
{
  /// <summary>
  /// Used to create a HttpClient that is used for performing callback notification.
  /// The default implementation (NotificationServiceHttpClientFactoryDefault) uses built in HttpClient
  /// Functional tests use another implementation that is connected to TestServer
  /// </summary>
  public interface INotificationServiceHttpClientFactory
  {
    HttpClient CreateClient(string clientName);
  }
}
