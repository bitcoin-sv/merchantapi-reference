// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.Common;
using MerchantAPI.Common.Database;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Rest
{
  public class Program
  {
    public static async Task Main(string[] args)
    {
      IHost host = CreateHostBuilder(args).Build();

      bool success;
      // Create a new scope
      using (var scope = host.Services.CreateScope())
      {
        var startup = scope.ServiceProvider.GetRequiredService<IStartupChecker>();
        success = await startup.CheckAsync();
      }

      // Run the WebHost, and start accepting requests
      if (success)
      {
        await host.RunAsync();
      }
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder =>
            {
              webBuilder.UseStartup<Startup>();
            })
            .ConfigureAppConfiguration((hostingContext, config) =>
            {
              config.AddJsonFile("providers.json", true);
            });
  }
}
