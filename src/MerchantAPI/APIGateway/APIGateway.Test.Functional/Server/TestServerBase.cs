// Copyright (c) 2020 Bitcoin Association

using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using MerchantAPI.APIGateway.Domain.Actions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MerchantAPI.APIGateway.Test.Functional.Server
{

  /// <summary>
  ///  HttpClient factory that is sued in unit tests and connected to TestServer
  /// </summary>
  public class NotificationServiceHttpClientFactoryTest : INotificationServiceHttpClientFactory
  {
    readonly TestServer testServer;
    public NotificationServiceHttpClientFactoryTest(TestServer testServer)
    {
      this.testServer = testServer ?? throw new ArgumentNullException(nameof(testServer));

    }
    public HttpClient CreateClient()
    {
      return testServer.CreateClient();
    }
  }

  public class TestServerBase
  {

    public static TestServer CreateServer(bool mockedServices, TestServer serverCallBack) 
    {
      var path = Assembly.GetAssembly(typeof(MapiServer))
        .Location;

      var hostBuilder = new WebHostBuilder()
        .UseContentRoot(Path.GetDirectoryName(path))
        .ConfigureAppConfiguration(cb =>
        {
          cb.AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.development.json", optional: true)
            .AddJsonFile("appsettings.test.functional.development.json", optional: true)
            .AddEnvironmentVariables();
        })
        .ConfigureLogging((context, logging) =>
        {
          logging.AddConfiguration(context.Configuration.GetSection("Logging"));
          logging.AddConsole();
          //logging.AddDebug();
        })
        .UseEnvironment("Testing");

      if (mockedServices)
      {
        hostBuilder.UseStartup<APIGatewayTestsStartup>();
      }
      else
      {
        hostBuilder.UseStartup<MerchantAPI.APIGateway.Rest.Startup>();
      }

      // Replace HttpClient for INotificationAction with one pointing towards test callback server
      // Alternative approach to this would be registering custom Http client as described here https://github.com/dotnet/aspnetcore/issues/21018 but that does not seem to work.
      if (serverCallBack != null)
      {
        hostBuilder.ConfigureTestServices(services =>
          {
            services.AddSingleton<INotificationServiceHttpClientFactory>((s) =>
              new NotificationServiceHttpClientFactoryTest(serverCallBack));
          }
        );
      }

      return new TestServer(hostBuilder);
    }
  }
}
