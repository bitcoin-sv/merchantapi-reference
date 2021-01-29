// Copyright (c) 2020 Bitcoin Association

using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MerchantAPI.Common.NotificationsHandler;
using MerchantAPI.Common.Startup;

namespace MerchantAPI.Common.Test
{
  public class CommonTestServerBase
  {

    public virtual TestServer CreateServer<TMapiServer, TestsStartup, RestStartup>(bool mockedServices, TestServer serverCallback, string dbConnectionString)
      where TMapiServer : class
    where TestsStartup : class
    where RestStartup : class
    {
      var path = Assembly.GetAssembly(typeof(TMapiServer))
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
        hostBuilder.UseStartup<TestsStartup>();
      }
      else
      {
        hostBuilder.UseStartup<RestStartup>();
      }

      // Replace HttpClient for INotificationAction with one pointing towards test callback server
      // Alternative approach to this would be registering custom Http client as described here https://github.com/dotnet/aspnetcore/issues/21018 but that does not seem to work.
      if (serverCallback != null)
      {
        hostBuilder.ConfigureTestServices(services =>
        {
          services.AddSingleton<INotificationServiceHttpClientFactory>((s) =>
            new NotificationServiceHttpClientFactoryTest(serverCallback));
          var serviceProvider = services.BuildServiceProvider();

          using var scope = serviceProvider.CreateScope();
          var scopedServices = scope.ServiceProvider;
          var startup = scopedServices.GetRequiredService<IStartupChecker>();
          CheckCreateDbAndClearDbAsync(startup, dbConnectionString).Wait();
        }
        );
      }

      return new TestServer(hostBuilder);
    }

    protected async Task CheckCreateDbAndClearDbAsync(IStartupChecker startup, string dbConnectionString)
    {
      await startup.CheckAsync(true);

      // delete database before each test
      CleanRepositories(dbConnectionString);
    }

    protected virtual void CleanRepositories(string dbConnectionString)
    {
      // override in derived class
    }

  }
}
