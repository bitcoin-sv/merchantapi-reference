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

namespace MerchantAPI.APIGateway.Test.Functional.CallBackWebServer
{
  /// <summary>
  /// Configures and starts web server that route POST requests to ICallBackReceived
  /// </summary>
  public static class CallBackServer
  {

    /// <summary>
    /// Starts an actual web server that can process callbacks
    /// </summary>
    public static IHost Start(string url, CancellationToken cancellationToken, ICallBackReceived callBackReceived)
    {

      var host = CreateHostBuilder(new string[0], url, callBackReceived).Build();
      _ = host.RunAsync(cancellationToken);
      return host;
    }

    /// <summary>
    /// Returns a TestServer that mocks HttpClient and can be used in unit tests
    /// </summary>
    public static TestServer GetTestServer(string url, ICallBackReceived callBackReceived)
    {
      var hostBuilder = CreateWebHostBuilder(url, callBackReceived);
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


    static void ConfigureWebHostBuilder(IWebHostBuilder webBuilder, string url, ICallBackReceived callBackReceived,
      bool logWebServerDetails = false)
    {

      var uri = new Uri(url);

      var hostAndPort = uri.Scheme + "://" + uri.Host + ":" + uri.Port;
      webBuilder.UseStartup<StressTestStartup>();
      webBuilder.UseUrls(hostAndPort);
      webBuilder.UseSetting("callback::url", url);
      webBuilder.ConfigureServices((s) => { s.AddSingleton(callBackReceived); });


      webBuilder.ConfigureAppConfiguration(cb =>
      {
        cb.AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(logWebServerDetails ? DebugConfig : NoMSLogConfig)));
      });

    }
    static IHostBuilder CreateHostBuilder(string[] args, string url, ICallBackReceived callBackReceived,
      bool logWebServerDetails = false)
    {
      return Host.CreateDefaultBuilder(args)
        .ConfigureWebHostDefaults(webBuilder => ConfigureWebHostBuilder(webBuilder, url, callBackReceived, logWebServerDetails));

    }

    static IWebHostBuilder CreateWebHostBuilder(string url, ICallBackReceived callBackReceived,
      bool logWebServerDetails = false)
    {
      var webBuilder = new WebHostBuilder();
      ConfigureWebHostBuilder(webBuilder, url, callBackReceived, logWebServerDetails);
      return webBuilder;
    }

  }
}
