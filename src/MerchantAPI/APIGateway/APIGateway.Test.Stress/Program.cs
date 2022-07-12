// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MerchantAPI.APIGateway.Domain.ViewModels;
using MerchantAPI.APIGateway.Rest.ViewModels;
using MerchantAPI.APIGateway.Test.Functional;
using MerchantAPI.Common.Test.CallbackWebServer;
using MerchantAPI.Common.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MerchantAPI.APIGateway.Infrastructure.Repositories;
using System.Diagnostics;

namespace MerchantAPI.APIGateway.Test.Stress
{

  class Program
  {
    private const string VERSION = "1.0.3";

    static readonly Random rnd = new();
    static async Task<int> SendTransactions(string configFileName, IHttpClientFactory httpClientFactory)
    {
      // Use Newtonsoft deserializer with default camel case policy:
      var config = HelperTools.JSONDeserializeNewtonsoft<SendConfig>(await File.ReadAllTextAsync(configFileName));

      var validationResults = new List<ValidationResult>();
      var validationContext = new ValidationContext(config, serviceProvider: null, items: null);
      if (!Validator.TryValidateObject(config, validationContext, validationResults, true))
      {
        var allErrors = string.Join(Environment.NewLine, validationResults.Select(x => x.ErrorMessage).ToArray());
        Console.WriteLine($"Invalid configuration {configFileName}. Errors: {allErrors}");
        return 0;
      }

      string[] GetAllCallbackUrls()
      {
        List <string> callbackUrls = new();
        if (config.MapiConfig.Callback?.AddRandomNumberToPort == null)
        {
          callbackUrls.Add(config.MapiConfig.Callback.Url);
        }
        else
        {
          for (int i = 1; i < config.MapiConfig.Callback.AddRandomNumberToPort.Value + 1; i++)
          {
            var uri = new UriBuilder(config.MapiConfig.Callback.Url);
            uri.Port += i;
            callbackUrls.Add(uri.ToString());
          }
        }

        return callbackUrls.ToArray();
      }

      string GetDynamicCallbackUrl()
      {
        if (config.MapiConfig.Callback?.AddRandomNumberToPort == null)
        {
          return config.MapiConfig.Callback?.Url;
        }

        var uri = new UriBuilder(config.MapiConfig.Callback.Url);

        uri.Port += rnd.Next(1, config.MapiConfig.Callback.AddRandomNumberToPort.Value + 1);
        return uri.ToString();
      }


      var transactions = new TransactionReader(config.Filename, config.TxIndex, config.Skip, config.Limit ?? long.MaxValue);

      var client = new HttpClient();
      if (config.MapiConfig.Authorization != null)
      {
        client.DefaultRequestHeaders.Add("Authorization", config.MapiConfig.Authorization);
      }

      Console.WriteLine($"*** Stress program v{ VERSION } ***");

      BitcoindProcess bitcoind = null;
      try
      {
        if (!string.IsNullOrEmpty(config.BitcoindConfig?.TemplateData))
        {

          bitcoind = await Utils.StartBitcoindWithTemplateDataAsync(config.BitcoindConfig.TemplateData, config.BitcoindConfig.BitcoindPath, config.BitcoindConfig.ZmqEndpointIp, httpClientFactory);
          Console.WriteLine("Bitcoind started successfully.");

          await Utils.EnsureMapiIsConnectedToNodeAsync(config.MapiConfig.MapiUrl, config.BitcoindConfig.MapiAdminAuthorization, config.MapiConfig.RearrangeNodes, bitcoind, config.MapiConfig.NodeHost, config.MapiConfig.NodeZMQNotificationsEndpoint);
        }

        await Utils.CheckFeeQuotesAsync(config.MapiConfig.AddFeeQuotesFromJsonFile, config.MapiConfig.MapiUrl, config.BitcoindConfig.MapiAdminAuthorization);

        await Utils.CheckFaultsAsync(config.MapiConfig.TestResilience?.AddFaultsFromJsonFile, config.MapiConfig.MapiUrl, config.BitcoindConfig.MapiAdminAuthorization);

        string mAPIversion = "";
        try
        {
          // test call and get mAPI version for CSV row
          var response = await client.GetAsync(config.MapiConfig.MapiUrl + "mapi/feeQuote");

          var responseAsString = await response.Content.ReadAsStringAsync();
          if (response.IsSuccessStatusCode)
          {
            var rEnvelope = HelperTools.JSONDeserialize<SignedPayloadViewModel>(responseAsString);
            var feeQuote = HelperTools.JSONDeserialize<FeeQuoteViewModelGet>(rEnvelope.Payload);
            mAPIversion = feeQuote.ApiVersion;
          }
        }
        catch (Exception e)
        {
          throw new Exception($"Can not connect to mAPI {config.MapiConfig.MapiUrl}. Check if parameters are correct. Error {e.Message}", e);
        }

        long missingTransactionsCountAtStart = 0;
        if (config.MapiConfig.TestResilience?.DBConnectionString != null)
        {
          // test DBConnectionString
          (var missingAtStart, _) = await Actions.GetMissingTransactionsAsync(config.MapiConfig.TestResilience.DBConnectionString, await measureGetRawMempoolAsync(false));
          missingTransactionsCountAtStart = missingAtStart.Length;
        }

        var stats = new Stats();

        // Start web server if required
        IHost[] webServers = null;
        var cancellationSource = new CancellationTokenSource();
        var cancellationBlocksSource = new CancellationTokenSource();
        if (config.MapiConfig.Callback?.StartListener == true)
        {
          var allUrls = GetAllCallbackUrls();
          webServers = new IHost[allUrls.Length];
          for(int i=0; i < allUrls.Length; i++)
          {
            Console.WriteLine($"Starting web server for url {allUrls[i]}");
            webServers[i] = CallbackServer.Start(allUrls[i], new CallbackReceived(stats, config.MapiConfig.Callback?.Hosts), cancellationSource.Token);
          }
        }


        async Task submitThreadAsync()
        {
          var batchSize = config.BatchSize;
          var batch = new List<string>();
          while (transactions.TryGetnextTransaction(out var transaction))
          {
            batch.Add(transaction);
            if (batch.Count >= batchSize)
            {
              await Actions.SendTransactionsBatch(batch, client, stats, config.MapiConfig.MapiUrl + "mapi/txs", GetDynamicCallbackUrl(), config.MapiConfig.Callback?.CallbackToken,
                config.MapiConfig.Callback?.CallbackEncryption);
              batch.Clear();
            }
          }

          // Send remaining transactions
          if (batch.Any())
          {
            await Actions.SendTransactionsBatch(batch, client, stats, config.MapiConfig.MapiUrl + "mapi/txs", GetDynamicCallbackUrl(), config.MapiConfig.Callback?.CallbackToken,
              config.MapiConfig.Callback?.CallbackEncryption);
            batch.Clear();
          }
        }

        async Task<string[]> measureGetRawMempoolAsync(bool generateCsvMempoolRow = true)
        {
          Stopwatch sw = Stopwatch.StartNew();
          var txsSubmitted = transactions.ReturnedCount;
          var mempool = await bitcoind.RpcClient.GetRawMempool();
          sw.Stop();
          if (generateCsvMempoolRow)
          {
            CsvUtils.GenerateMempoolCsvRow(txsSubmitted, mempool.Length, sw.Elapsed);
          }
          return mempool;
        }

        async Task MempoolCheckTask(CancellationToken cancellationToken)
        {
          int incrementStep = config.GetRawMempoolEveryNTxs;
          long goal = incrementStep;
          Stopwatch sw = new();
          while (goal <= config.Limit)
          {
            if (transactions.ReturnedCount >= goal)
            {
              try
              {
                await measureGetRawMempoolAsync();
                goal += incrementStep;
                if (cancellationToken.IsCancellationRequested)
                {
                  return;
                }
              }
              catch (Exception ex)
              {
                Console.WriteLine("Error calling GetRawMempool: " + ex.Message);
              }
              finally
              {
                sw.Reset();
              }
            }
            await Task.Delay(10, CancellationToken.None);
          }
        }

        async Task GenerateBlock(CancellationToken cancellationToken, int generateBlockPeriodMs)
        {
          do
          {
            try
            {
              await bitcoind.RpcClient.GenerateAsync(1);
              Interlocked.Increment(ref stats.GenerateBlockCalls);
            }
            catch (Exception ex)
            {
              Console.WriteLine("Error generating block: " + ex.Message);
            }

            await Task.Delay(generateBlockPeriodMs, cancellationToken);
          } while (!cancellationToken.IsCancellationRequested);
        }

        async Task PrintProgress(CancellationToken cancellationToken)
        {
          do
          {
            Console.WriteLine(stats);
            await Task.Delay(1000, cancellationToken);

          } while (!cancellationToken.IsCancellationRequested);

        }

        void RunSubmitThreads()
        {
          var tasks = new List<Task>();
          for (int i = 0; i < config.Threads; i++)
          {
            tasks.Add(Task.Run(submitThreadAsync));
          }
          Task.WaitAll(tasks.ToArray());
        }


        if (config.StartGenerateBlocksAtTx > -1)
        {
          transactions.SetLimit(config.StartGenerateBlocksAtTx);
        }

        Console.WriteLine($"Starting {config.Threads} concurrent tasks");

        var progressTask = Task.Run(() => PrintProgress(cancellationSource.Token), cancellationSource.Token);

        Task mempoolCheckTask = null;

        var results = new List<(int mempoolCount, TimeSpan elapsed)>();

        bool measureRawMempoolCall = config.GetRawMempoolEveryNTxs > 0;
        if (measureRawMempoolCall)
        {
          // check mempool before anything is submitted
          await measureGetRawMempoolAsync();

          mempoolCheckTask = Task.Run(() => MempoolCheckTask(cancellationSource.Token), cancellationSource.Token);
        }

        RunSubmitThreads();

        stats.StopTiming(); // we are no longer submitting txs. Stop the Stopwatch that is used to calculate submission throughput

        Task generateTask = null;

        if (config.StartGenerateBlocksAtTx > -1)
        {
          transactions.SetLimit(config.Limit ?? long.MaxValue);

          stats.ResumeTiming();

          Console.WriteLine($"Starting {config.Threads} concurrent tasks again together with generate blocks");

          generateTask = Task.Run(() => GenerateBlock(cancellationBlocksSource.Token, config.GenerateBlockPeriodMs), cancellationBlocksSource.Token);

          RunSubmitThreads();

          stats.StopTiming();
        }

        // Cancel submit and generateBlock tasks
        cancellationBlocksSource.Cancel(false);
        try
        {
          if (generateTask != null)
          {
            generateTask.Wait();
          }
        }
        catch
        {
        }

        Console.WriteLine("Finished sending transactions.");

        if (config.MapiConfig.Callback?.StartListener == true && bitcoind != null &&
           !string.IsNullOrEmpty(config.MapiConfig.Callback?.Url) && stats.OKSubmitted > 0)
        {
          if (stats.GenerateBlockCalls == 0)
          {
            Console.WriteLine("Will generate a block to trigger callbacks");
            await bitcoind.RpcClient.GenerateAsync(1);
            Interlocked.Increment(ref stats.GenerateBlockCalls);
          }
          await Actions.WaitForCallbacksAsync(config.MapiConfig.Callback.IdleTimeoutMS, stats);
        }

        // Cancel progress task
        cancellationSource.Cancel(false);
        try
        {
          progressTask.Wait();
        }
        catch
        {
        }

        if (webServers != null)
        {
          foreach (var webServer in webServers)
          {
            await webServer.StopAsync();
          }
        }
        // GetRawMempool after all txs are submitted
        var mempoolTxs = await measureGetRawMempoolAsync(measureRawMempoolCall);
        CsvUtils.GenerateCsvRow(mAPIversion, mempoolTxs.Length, config, stats);

        if (stats.RequestErrors > 0)
        {
          Console.WriteLine($"There were request errors with code 500 present. It is possible, that some transactions are in node's mempool, but not in DB.");
          await measureGetRawMempoolAsync(false);
        }

        if (config.MapiConfig.TestResilience?.DBConnectionString != null)
        {
          int countNoDiff = 0;
          var txsCount = await Actions.GetAllSuccessfulTransactionsAsync(config.MapiConfig.TestResilience.DBConnectionString);
          Actions.PrintToConsoleWithColor(
            $"All transactions in database, that should be in mempool or blockchain: { txsCount }.",
            ConsoleColor.Yellow);
          long missing = (await Actions.GetMissingTransactionsAsync(config.MapiConfig.TestResilience.DBConnectionString, await measureGetRawMempoolAsync(false))).missingTxs.Length;
          while (missing > 0 && countNoDiff < 10)
          {
            await Task.Delay(1500);
            // Wait for resubmit of missing transactions
            (var missingTxs, _) = await Actions.GetMissingTransactionsAsync(config.MapiConfig.TestResilience.DBConnectionString, await measureGetRawMempoolAsync(false));
            countNoDiff = (missingTxs.Length >= missing) ? ++countNoDiff : 0;
            missing = missingTxs.Length;
          }
          if (missing == 0)
          {
            Console.WriteLine($"Mempool checker sucessfully finished (or nothing was missing).");
          }
          else
          {
            Actions.PrintToConsoleWithColor(
              $@"There is a problem with resubmit. Please check that mempoolChecker is enabled and MempoolCheckerIntervalSec is 10 or less.{Environment.NewLine}
Also check that blocks parsing is enabled and that bitcoind's maxmempool setting is not too low.",
              ConsoleColor.Yellow);
          }
        }

        if (config.MapiConfig.TestResilience?.ResubmitWithoutFaults == true)
        {
          Console.WriteLine("Test resubmit without faults:");
          await Utils.ClearAllFaultsAsync(config.MapiConfig.TestResilience?.AddFaultsFromJsonFile, config.MapiConfig.MapiUrl, config.BitcoindConfig.MapiAdminAuthorization);

          stats = new Stats();
          var cancellationSourceResubmit = new CancellationTokenSource();
          // resubmit all (check for duplicates is fast, so it doesn't take that long as the first time)
          transactions = new TransactionReader(config.Filename, config.TxIndex, config.Skip, config.Limit ?? long.MaxValue);
          var progressTaskResubmit = Task.Run(() => PrintProgress(cancellationSourceResubmit.Token), cancellationSourceResubmit.Token);

          RunSubmitThreads();
          stats.StopTiming();

          // Cancel progress task
          cancellationSourceResubmit.Cancel(false);
          try
          {
            progressTaskResubmit.Wait();
          }
          catch
          {
          }
          // GetRawMempool after all txs are resubmitted
          mempoolTxs = await measureGetRawMempoolAsync(false);
          CsvUtils.GenerateCsvRow(mAPIversion, mempoolTxs.Length, config, stats);
          var txsCount = await Actions.GetAllSuccessfulTransactionsAsync(config.MapiConfig.TestResilience.DBConnectionString);
          Actions.PrintToConsoleWithColor(
            $"All transactions that are marked as successful in DB: { txsCount }. All transactions submitted: {transactions.ReturnedCount}",
            txsCount == transactions.ReturnedCount ? ConsoleColor.Green : ConsoleColor.Yellow);
        }
      }
      catch (Exception ex)
      {
        Actions.PrintToConsoleWithColor(
          $"Program interrupted, please check the exception below: { Environment.NewLine }{ ex }",
          ConsoleColor.Red);
      }
      finally
      {
        bitcoind?.Dispose();
      }

      return 0;
    }


    static async Task<int> ClearDb(string mapiDBConnectionString, bool eraseFeeQuotes = false)
    {
      await Actions.CleanUpTxHandler(mapiDBConnectionString);
      NodeRepositoryPostgres.EmptyRepository(mapiDBConnectionString);
      if (eraseFeeQuotes)
      {
        FeeQuoteRepositoryPostgres.EmptyRepository(mapiDBConnectionString);
        Console.WriteLine($"Table feeQuote truncated.");
      }
      Console.WriteLine($"Tables truncated.");
      Console.WriteLine($"mAPI cache is out of sync, you have to restart mAPI!");
      return 0;
    }

    static async Task<int> Main(string[] args)
    {
      var builder = new HostBuilder()
               .ConfigureServices((hostContext, services) =>
               {
                 services.AddHttpClient();
               }).UseConsoleLifetime();
      var host = builder.Build();

      var sendCommand = new Command("send")
      {
        new Argument<string>(
          name: "configFileName",
          description: "Json config file containing configuration"
        )
        {
          Arity = new ArgumentArity(1,1)
        }
      };

      sendCommand.Description = "Read transactions from a json file and submit it to mAPI.";
      sendCommand.Handler = CommandHandler.Create(async (string configFileName) =>
        await SendTransactions(configFileName, (IHttpClientFactory)host.Services.GetService(typeof(IHttpClientFactory))));

      var clearDbCommand = new Command("clearDb")
      {
        new Argument<string>(
          name: "mapiDBConnectionStringDDL",
          description: "Connection string DDL, e.g.'Server=localhost;Port=54321;User Id=merchantddl; Password=merchant;Database=merchant_gateway;'"
        )
        {
          Arity = new ArgumentArity(1,1)
        },
      };
      clearDbCommand.AddOption(
        new Option<bool?>(
          alias: "eraseFeeQuotes",
          getDefaultValue: () => false,
          description: "False by default - set to true if you also want to truncate feeQuote table."
        )
      );

      clearDbCommand.Description = "Truncate data in all tables (feeQuote table is only truncated, if second argument is defined as true) and stops program. If mAPI is running, restart it to reinitialize cache.";
      clearDbCommand.Handler = CommandHandler.Create(async (string mapiDBConnectionString, bool eraseFeeQuotes) =>
        await ClearDb(mapiDBConnectionString, eraseFeeQuotes));

      var rootCommand = new RootCommand
      {
        sendCommand,
        clearDbCommand
      };

      rootCommand.Description = "mAPI stress test";

      return await rootCommand.InvokeAsync(args);
    }

  }
}