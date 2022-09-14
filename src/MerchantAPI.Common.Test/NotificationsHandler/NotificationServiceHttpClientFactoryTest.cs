// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.Common.NotificationsHandler;
using Microsoft.AspNetCore.TestHost;
using System;
using System.Net.Http;

namespace MerchantAPI.Common.Test.NotificationsHandler
{
  /// <summary>
  ///  HttpClient factory that is used in unit tests and connected to TestServer
  /// </summary>
  public class NotificationServiceHttpClientFactoryTest : INotificationServiceHttpClientFactory
  {
    readonly TestServer testServer;
    public NotificationServiceHttpClientFactoryTest(TestServer testServer)
    {
      this.testServer = testServer ?? throw new ArgumentNullException(nameof(testServer));

    }

    public HttpClient CreateClient(string clientName)
    {
      return testServer.CreateClient();
    }
  }
}
