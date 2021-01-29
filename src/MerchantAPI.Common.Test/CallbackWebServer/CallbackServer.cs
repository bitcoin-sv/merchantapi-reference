// Copyright (c) 2020 Bitcoin Association

using System;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MerchantAPI.Common.Test.CallbackWebServer
{
  /// <summary>
  /// Configures and starts web server that route POST requests to ICallbackReceived
  /// </summary>
  public static class CallbackServer
  {

    /// <summary>
    /// Starts an actual web server that can process callbacks
    /// </summary>
    public static IHost Start(string url, CancellationToken cancellationToken, ICallbackReceived callbackReceived)
    {

      var host = CreateHostBuilder(new string[0], url, callbackReceived).Build();
      _ = host.RunAsync(cancellationToken);
      return host;
    }

    /// <summary>
    /// Returns a TestServer that mocks HttpClient and can be used in unit tests
    /// </summary>
    public static TestServer GetTestServer(string url, ICallbackReceived callbackReceived)
    {
      var hostBuilder = CreateWebHostBuilder(url, callbackReceived);
      return new TestServer(hostBuilder);
    }


    private const string DebugConfig =
@"{
  ""Logging"": {
    ""LogLevel"": {
      ""Default"": ""Information"",
      ""Microsoft"": ""Information"",
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


    static void ConfigureWebHostBuilder(IWebHostBuilder webBuilder, string url, ICallbackReceived callbackReceived,
      bool logWebServerDetails = false)
    {

      var uri = new Uri(url);

      var hostAndPort = uri.Scheme + "://" + uri.Host + ":" + uri.Port;
      webBuilder.UseStartup<StressTestStartup>();
      webBuilder.UseUrls(hostAndPort);
      webBuilder.UseSetting("callback::url", url);
      webBuilder.ConfigureServices((s) => { s.AddSingleton(callbackReceived); });


      webBuilder.ConfigureAppConfiguration(cb =>
      {
        cb.AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(logWebServerDetails ? DebugConfig : NoMSLogConfig)));
      });

    }
    static IHostBuilder CreateHostBuilder(string[] args, string url, ICallbackReceived callbackReceived,
      bool logWebServerDetails = false)
    {
      return Host.CreateDefaultBuilder(args)
        .ConfigureWebHostDefaults(webBuilder => ConfigureWebHostBuilder(webBuilder, url, callbackReceived, logWebServerDetails));

    }

    static IWebHostBuilder CreateWebHostBuilder(string url, ICallbackReceived callbackReceived,
      bool logWebServerDetails = false)
    {
      var webBuilder = new WebHostBuilder();
      ConfigureWebHostBuilder(webBuilder, url, callbackReceived, logWebServerDetails);
      return webBuilder;
    }

  }
}
