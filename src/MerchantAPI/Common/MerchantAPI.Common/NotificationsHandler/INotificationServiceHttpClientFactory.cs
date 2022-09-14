// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System.Net.Http;

namespace MerchantAPI.Common.NotificationsHandler
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
