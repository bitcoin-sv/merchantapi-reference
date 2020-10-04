// Copyright (c) 2020 Bitcoin Association

using System;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MerchantAPI.APIGateway.Test.Functional.CallBackWebServer
{
  /// <summary>
  /// Configures and starts web server that route POST requests to ICallBackReceived
  /// </summary>
  public static class StartWebServer
  {

    public static IHost Start(string url, CancellationToken cancellationToken, ICallBackReceived callBackReceived)
    {

      var host = CreateHostBuilder(new string[0], url, callBackReceived).Build();
      _ = host.RunAsync(cancellationToken);
      return host;
    }


    private const string DebugConfig =
@"{
  ""Logging"": {
    ""LogLevel"": {
      ""Default"": ""Information"",
      ""Microsoft"": ""Information"",
      ""Microsoft.Hosting.Lifetime"": ""Information""
    }
  }
}";

    private const string NoMSLogConfig =
      @"{
  ""Logging"": {
    ""LogLevel"": {
      ""Default"": ""Information"",
      ""Microsoft"": ""None"",
      ""Microsoft.Hosting.Lifetime"": ""None""
    }
  }
}";


    static IHostBuilder CreateHostBuilder(string[] args, string url, ICallBackReceived callBackReceived,
      bool logWebServerDetails = false)
    {

      var uri = new Uri(url);

      var hostAndPort = uri.Scheme + "://" + uri.Host + ":" + uri.Port;
      return Host.CreateDefaultBuilder(args)
        .ConfigureWebHostDefaults(webBuilder =>
        {
          webBuilder.UseStartup<StressTestStartup>();
          webBuilder.UseUrls(hostAndPort);
          webBuilder.UseSetting("callback::url", url);
          webBuilder.ConfigureServices((s) => { s.AddSingleton(callBackReceived); });

          
          webBuilder.ConfigureAppConfiguration(cb =>
          {
            cb.AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(logWebServerDetails ? DebugConfig : NoMSLogConfig)));
          });

        });

    }

  }
}
