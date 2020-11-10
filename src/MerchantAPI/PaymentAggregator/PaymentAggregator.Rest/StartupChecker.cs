// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.Common;
using MerchantAPI.Common.Database;
using MerchantAPI.PaymentAggregator.Domain.Client;
using MerchantAPI.PaymentAggregator.Domain.Repositories;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MerchantAPI.PaymentAggregator.Rest
{
  public class StartupChecker : IStartupChecker
  {
    readonly IGatewayRepository gatewayRepository;
    readonly ICreateDB createDB;
    readonly IApiGatewayClientFactory apiGatewayClientFactory;
    readonly ILogger<StartupChecker> logger;
    readonly RDBMS rdbms;

    public StartupChecker(IGatewayRepository gatewayRepository,
                          ICreateDB createDB,
                          IApiGatewayClientFactory apiGatewayClientFactory,
                          ILogger<StartupChecker> logger)
    {
      this.gatewayRepository = gatewayRepository ?? throw new ArgumentNullException(nameof(gatewayRepository));
      this.logger = logger ?? throw new ArgumentException(nameof(logger));
      this.createDB = createDB ?? throw new ArgumentException(nameof(createDB));
      this.apiGatewayClientFactory = apiGatewayClientFactory ?? throw new ArgumentNullException(nameof(apiGatewayClientFactory));
      rdbms = RDBMS.Postgres;
    }

    public async Task<bool> CheckAsync(bool testingEnvironment)
    {
      logger.LogInformation("Health checks starting.");
      try
      {
        RetryUtils.ExecAsync(() => TestDBConnection(), retry: 10, errorMessage: "Unable to open connection to database").Wait();
        ExecuteCreateDb();
        if (!testingEnvironment)
        {
          await TestGatewaysConnectivityAsync();
        }
        logger.LogInformation("Health checks completed successfully.");
      }
      catch (Exception ex)
      {
        logger.LogError("Health checks failed. {0}", ex.GetBaseException().ToString());
        // If exception was thrown then we stop the application. All methods in try section must pass without exception
        if (testingEnvironment)
        {
          throw;
        }
        return false;
      }

      return true;
    }


    private Task TestDBConnection()
    {
      bool databaseExists = createDB.DatabaseExists("PaymentAggregator", rdbms);
      if (databaseExists)
      {
        logger.LogInformation($"Successfully connected to DB.");
      }
      return Task.CompletedTask;
    }


    private void ExecuteCreateDb()
    {
      logger.LogInformation($"Starting with execution of CreateDb ...");


      if (createDB.DoCreateDB("PaymentAggregator", rdbms, out string errorMessage, out string errorMessageShort))
      {
        logger.LogInformation("CreateDB finished successfully.");
      }
      else
      {
        // if error we must stop application
        throw new Exception($"Error when executing CreateDB: { errorMessage }{ Environment.NewLine }ErrorMessage: {errorMessageShort}");
      }

      logger.LogInformation($"ExecuteCreateDb completed.");
    }

    private async Task TestGatewaysConnectivityAsync()
    {
      logger.LogInformation($"Checking gateways connectivity");

      bool success = false;
      var gateways = gatewayRepository.GetGateways(true);
      foreach (var gateway in gateways)
      {
        try
        {
          // test call public getFeeQuote
          using CancellationTokenSource cts = new CancellationTokenSource(2000);
          await apiGatewayClientFactory.Create(gateway.Url).TestMapiFeeQuoteAsync(cts.Token);
          success = true;
        }
        catch (Exception)
        {
          logger.LogWarning($"Gateway with id: '{gateway.Id}' and url: '{gateway.Url}' is unreachable");
        }
      }
      if (!gateways.Any())
      {
        logger.LogWarning("There are no active gateways present in database.");
      }
      else if (!success)
      {
        logger.LogWarning($"There are active gateways present but none were successfully called");
      }
      logger.LogInformation($"Gateways connectivity check complete");
    }

  }
}
