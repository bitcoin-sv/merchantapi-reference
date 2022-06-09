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
using System.Net.Mime;
using System.Text;
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
using MerchantAPI.APIGateway.Domain.Models;
using Newtonsoft.Json;

namespace MerchantAPI.APIGateway.Test.Stress
{

  class Program
  {
    private const string VERSION = "1.0.2";

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


      string GetDynamicCallbackUrl()
      {
        if (config.MapiConfig.Callback?.AddRandomNumberToHost == null)
        {
          return config.MapiConfig.Callback?.Url;
        }

        var uri = new UriBuilder(config.MapiConfig.Callback.Url);

        uri.Host += rnd.Next(1, config.MapiConfig.Callback.AddRandomNumberToHost.Value + 1);
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

        var stats = new Stats();


        // Start web server if required
        IHost webServer = null;
        var cancellationSource = new CancellationTokenSource();
        var cancellationBlocksSource = new CancellationTokenSource();
        if (config.MapiConfig.Callback?.StartListener == true)
        {
          Console.WriteLine($"Starting web server for url {config.MapiConfig.Callback.Url}");
          webServer = CallbackServer.Start(config.MapiConfig.Callback.Url, new CallbackReceived(stats, config.MapiConfig.Callback?.Hosts), cancellationSource.Token);
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

        async Task<int> measureGetRawMempoolAsync(bool generateCsvMempoolRow = true)
        {
          Stopwatch sw = Stopwatch.StartNew();
          var txsSubmitted = transactions.ReturnedCount;
          var mempool = await bitcoind.RpcClient.GetRawMempool();
          sw.Stop();
          if (generateCsvMempoolRow)
          {
            CsvUtils.GenerateMempoolCsvRow(txsSubmitted, mempool.Length, sw.Elapsed);
          }
          return mempool.Length;
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

        if (webServer != null)
        {
          await webServer.StopAsync();
        }
        // GetRawMempool after all txs are submitted
        var mempoolTxs = await measureGetRawMempoolAsync(measureRawMempoolCall);
        CsvUtils.GenerateCsvRow(mAPIversion, mempoolTxs, config, stats);
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