// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.APIGateway.Test.Functional.Server;
using MerchantAPI.Common.Database;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql.Logging;
using System;

namespace MerchantAPI.APIGateway.Test.Functional
{
  // run code before all unit tests in an assembly executes
  [TestClass]
  public class SetupTestAssemblyInitializer
  {
    private static bool setProvider = false;

    [AssemblyInitialize]
    public static void AssemblyInit(TestContext context)
    {
      // Initialization code goes here
      if (!setProvider)
      {
        NpgsqlLogManager.Provider = new ConsoleLoggingProvider(NpgsqlLogLevel.Debug);
        NpgsqlLogManager.IsParameterLoggingEnabled = true;
        setProvider = true;
      }

      //setup server
      var server = TestServerBase.CreateServer(false, null);
      var loggerFactory = server.Services.GetRequiredService<ILoggerFactory>();
      var loggerTest = loggerFactory.CreateLogger(TestBase.LOG_CATEGORY);

      var createDB = server.Services.GetRequiredService<ICreateDB>();
      bool success = createDB.DoCreateDB("APIGateway", RDBMS.Postgres, out string errorMessage, out string errorMessageShort);

      if (success)
      {
        loggerTest.LogInformation($"CreateDB in { context.FullyQualifiedTestClassName } finished successfully.");
      }
      else
      {
        loggerTest.LogError($"Error when executing CreateDB in { context.FullyQualifiedTestClassName } : { errorMessage }{ Environment.NewLine }ErrorMessage: {errorMessageShort}");
      }
      // if error we must stop execution of test 
      Assert.IsTrue(success);

      server?.Dispose();
    }
  }
}
