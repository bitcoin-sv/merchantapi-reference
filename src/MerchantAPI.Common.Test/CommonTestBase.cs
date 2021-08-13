// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using Dapper;
using MerchantAPI.Common.Authentication;
using MerchantAPI.Common.Test.CallbackWebServer;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MerchantAPI.Common.Test
{
  public abstract class CommonTestBase<TAppSettings>
    where TAppSettings : CommonAppSettings, new()

  {
    public TAppSettings AppSettings;
    public IConfigurationRoot Configuration { get; private set; }

    /// <summary>
    /// When set it is used as Rest authentication header in tests
    /// </summary>
    public string RestAuthentication { get; set; }

    /// <summary>
    /// When set it is used as API Key Rest authentication header in tests
    /// </summary>
    public string ApiKeyAuthentication { get; set; }

    protected TestServer server;
    public HttpClient client { get; set; }

    protected TestServer serverCallback;
    protected HttpClient clientCallback;

    protected ILogger loggerTest;
    protected ILoggerFactory loggerFactory;

    public virtual string LOG_CATEGORY { get; }

    private static bool providerSet;

    public virtual string DbConnectionString { get; }

    public static AutoResetEvent SyncTest = new AutoResetEvent(true);

    public CallbackFunctionalTests Callback = new CallbackFunctionalTests();

    protected UserAndIssuer MockedIdentity
    {
      get
      {
        return new UserAndIssuer() { Identity = "5", IdentityProvider = "http://mysite.com" };
      }
    }

    protected string MockedIdentityToken
    {
      get
      {
        // TokenManager.exe generate -n 5 -i http://mysite.com -a http://myaudience.com -k thisisadevelopmentkey -d 3650
        return "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiI1IiwibmJmIjoxNTk5NDExNDQzLCJleHAiOjE5MTQ3NzE0NDMsImlhdCI6MTU5OTQxMTQ0MywiaXNzIjoiaHR0cDovL215c2l0ZS5jb20iLCJhdWQiOiJodHRwOi8vbXlhdWRpZW5jZS5jb20ifQ.Z43NASAbIxMZrL2MzbJTJD30hYCxhoAs-8heDjQMnjM";
      }
    }

    protected string MockedIdentityBearerAuthentication
    {
      get
      {
        return GetBearerAuthentication(MockedIdentityToken);
      }
    }

    protected string GetBearerAuthentication(string token)
    {
      return $"Bearer { token }";
    }

    public async Task WaitUntilAsync(Func<bool> predicate, int timeOutSeconds = 10)
    {
      var start = DateTime.UtcNow;

      while ((DateTime.UtcNow - start).TotalSeconds < timeOutSeconds)
      {
        if (predicate())
        {
          return;
        }

        await Task.Delay(1000);
      }
      // Output log entry before throwing to make it easier to pinpoint exact position of failure in log file
      loggerTest.LogError("WaitUntilAsync: condition was not satisfied in prescribed period - will throw.");
      throw new Exception("WaitUntilAsync: timeout expired. The conditions was not satisfied in prescribed period.");
    }


    public async Task WaitUntilAsync(Func<Task<bool>> predicate, int timeOutSeconds = 10)
    {
      var start = DateTime.UtcNow;

      while ((DateTime.UtcNow - start).TotalSeconds < timeOutSeconds)
      {
        if (await predicate())
        {
          return;
        }

        await Task.Delay(1000);
      }

      throw new Exception("WaitUntilAsync: timeout expired. The conditions was not satisfied in prescribed period.");
    }


    public CommonTestBase()
    {
      if (!providerSet)
      {
        // uncomment if needed
        //NpgsqlLogManager.Provider = new ConsoleLoggingProvider(NpgsqlLogLevel.Debug);
        //NpgsqlLogManager.IsParameterLoggingEnabled = true;
        providerSet = true;
      }

      SqlMapper.AddTypeHandler(new Common.TypeHandlers.DateTimeHandler());

      string appPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
      Configuration = new ConfigurationBuilder()
        .AddJsonFile(Path.Combine(appPath, "appsettings.json"))
        .AddJsonFile(Path.Combine(appPath, "appsettings.development.json"), optional: true)
        .AddJsonFile(Path.Combine(appPath, "appsettings.test.functional.development.json"), optional: true)
        .AddEnvironmentVariables()
        .Build();


      AppSettings = Configuration.GetSection("AppSettings").Get<TAppSettings>();

    }

    abstract public TestServer CreateServer(bool mockedServices, TestServer serverCallback, string dbConnectionString, IEnumerable<KeyValuePair<string, string>> overridenSettings = null);

    public virtual void Initialize(bool mockedServices = false, IEnumerable<KeyValuePair<string, string>> overridenSettings = null)
    {
      SyncTest.WaitOne(); // tests must not run in parallel since each test first deletes database
      try
      {


        // setup call back server
        serverCallback = CallbackServer.GetTestServer(Callback.Url, Callback);
        clientCallback = serverCallback.CreateClient();

        //setup server
        server = this.CreateServer(mockedServices, serverCallback, DbConnectionString, overridenSettings);
        client = server.CreateClient();

        // setup common services
        loggerFactory = server.Services.GetRequiredService<ILoggerFactory>();
        loggerTest = loggerFactory.CreateLogger(LOG_CATEGORY);

        loggerTest.LogInformation($"Path: {Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)}");
        loggerTest.LogInformation($"ConnString: {DbConnectionString}");
      }
      catch
      {
        SyncTest.Reset();
        // If there was error during initialization, let the other tests run (although they will probably also fail)
        throw;
      }

    }

    public void Cleanup(Action afterServerDisposed = null)
    {
      loggerTest?.LogInformation("Starting test cleanup");
      server?.Dispose();
      loggerTest?.LogInformation("TestServer disposed");

      afterServerDisposed?.Invoke();
      SyncTest.Set();
      loggerTest?.LogInformation("Test cleanup finished");
    }
  }
}
