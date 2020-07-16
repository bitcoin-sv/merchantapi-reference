// Copyright (c) 2020 Bitcoin Association

using System.IO;
using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MerchantAPI.APIGateway.Test.Functional.Server
{
  public class TestServerBase
  {
    public static TestServer CreateServer(bool mockedServices) 
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

      return new TestServer(hostBuilder);
    }
  }
}
