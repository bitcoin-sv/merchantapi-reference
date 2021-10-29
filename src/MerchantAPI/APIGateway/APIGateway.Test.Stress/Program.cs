// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net;
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
using Microsoft.Extensions.Logging.Abstractions;
using NBitcoin;
using Microsoft.Extensions.DependencyInjection;
using CsvHelper;
using System.Globalization;
using CsvHelper.Configuration;

namespace MerchantAPI.APIGateway.Test.Stress
{

  class Program
  {
 
    static async Task SendTransactionsBatch(IEnumerable<string> transactions, HttpClient client, Stats stats, string url, string callbackUrl, string callbackToken, string callbackEncryption)
    {

      var query = new List<string>();

      string doCallbacks = string.IsNullOrEmpty(callbackUrl) ? "false" : "true";
      query.Add($"defaultDsCheck={doCallbacks}");
      query.Add($"defaultMerkleProof={doCallbacks}");

      if (!string.IsNullOrEmpty(callbackUrl))
      {
        query.Add("defaultCallbackUrl=" + WebUtility.UrlEncode(callbackUrl));

        if (!string.IsNullOrEmpty(callbackToken))
        {
          query.Add("defaultCallbackToken=" + WebUtility.UrlEncode(callbackToken));
        }

        if (!string.IsNullOrEmpty(callbackEncryption))
        {
          query.Add("defaultCallbackEncryption=" + WebUtility.UrlEncode(callbackEncryption));
        }
      }

      string queryString = string.Join("&", query.ToArray());

      var ub = new UriBuilder(url);
      if (ub.Query.Length == 0)
      {
        ub.Query = queryString; // automatically adds ? at the beginning
      }
      else
      {
        ub.Query = ub.Query[1..] + "&" + queryString; // remove leading ?  it is added back automatically
      }

      string urlWithParams = ub.Uri.ToString();

      string callbackHost = "";
      if (!string.IsNullOrEmpty(callbackUrl))
      {
        callbackHost = new Uri(callbackUrl).Host;
      }


      // We currently submit through REST interface., We could also use binary  interface
      var request = transactions.Select(t => new SubmitTransactionViewModel
      {
        RawTx = t,
        // All other parameters are passed in query string
        CallbackUrl = null, 
        CallbackToken = null,
        CallbackEncryption = null,
        MerkleProof = null,
        DsCheck = null
      }).ToArray();

      var requestString = HelperTools.JSONSerialize(request, false);
      var response = await client.PostAsync(urlWithParams,
        new StringContent(requestString, new UTF8Encoding(false), MediaTypeNames.Application.Json));

      var responseAsString = await response.Content.ReadAsStringAsync();
      if (!response.IsSuccessStatusCode)
      {
        Console.WriteLine($"Error while submitting transaction request {responseAsString}");
        stats.IncrementRequestErrors();
      }
      else
      {
        var rEnvelope = HelperTools.JSONDeserialize<SignedPayloadViewModel>(responseAsString);
        var r = HelperTools.JSONDeserialize<SubmitTransactionsResponseViewModel>(rEnvelope.Payload);
        int printLimit = 10;
        var errorItems = r.Txs.Where(t => t.ReturnResult != "success").ToArray();

        var okItems = r.Txs.Where(t => t.ReturnResult == "success").ToArray();

        stats.AddRequestTxFailures(callbackHost, errorItems.Select(x=> new uint256(x.Txid)));
        stats.AddOkSubmited(callbackHost, okItems.Select(x => new uint256(x.Txid)));

        var errors = errorItems
          .Select(t => t.Txid + " " + t.ReturnResult + " " + t.ResultDescription).ToArray();



        var limitedErrors = string.Join(Environment.NewLine, errors.Take(printLimit));
        if (errors.Any())
        {
          Console.WriteLine($"Error while submitting transactions. Printing  up to {printLimit} out of {errors.Length} errors : {limitedErrors}");
        }
      }

    }

    static async Task<BitcoindProcess> StartBitcoindWithTemplateDataAsync(string templateData, string bitcoindPath, IHttpClientFactory httpClientFactory)
    {
      var testDataDir = CopyTemplateData(templateData);


      Console.WriteLine("Starting up bitcoind");

      if (string.IsNullOrEmpty(bitcoindPath))
      {
        bitcoindPath = Path.Combine(Directory.GetCurrentDirectory(), "bitcoind");
        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
          bitcoindPath += ".exe";
        }
      }

      if (!File.Exists(bitcoindPath))
      {
        throw new Exception($"Can not start bitcoind. Expected bitcoind at location: {bitcoindPath}");
      }

      var bitcoind = new BitcoindProcess("localhost", bitcoindPath, testDataDir, 18444, 18332, "127.0.0.1", 28333,
        new NullLoggerFactory(), httpClientFactory, emptyDataDir: false);

      long blocks = (await bitcoind.RpcClient.GetBlockchainInfoAsync()).Blocks;

      if (blocks == 0)
      {
        Console.WriteLine($"Warning: current active chain has 0 blocks. The submission of all transactions will probably fail. Check the content of template data directory - {templateData}");
      }

      return bitcoind;

    }
    /// <summary>
    /// Wait until all callback are received or until timeout expires
    /// Print out any missing callbacks
    /// </summary>
    static async Task WaitForCallbacksAsync(int timeoutMs, Stats stats)
    {

      (string host, uint256[] txs)[] missing;
      bool timeout;
      do
      {
        await Task.Delay(1000);
        missing = stats.GetMissingCallbacksByHost();
        timeout = stats.LastUpdateAgeMs > timeoutMs;

      } while (missing.Any() && !timeout);

      if (timeout)
      {
        Console.WriteLine($"Timeout occurred when waiting for callbacks. No new callbacks received for last {timeoutMs} ms");
      }
      
      // TODO: print out multiple callbacks
      if (missing.Any())
      {
        const int printUpTo = 3;
        Console.WriteLine($"Error: Not all callback were received. Total missing {missing.Sum(x=> x.txs.Length)}");
        Console.WriteLine($"Printing up to {printUpTo} missing tx per host");
        foreach (var host in missing)
        {
          Console.WriteLine($"   {host.host}  {string.Join(" ",host.txs.Take(printUpTo).Select(x=> x.ToString()).ToArray())} ");
        }
      }
      else
      {
        Console.WriteLine("Ok, all callbacks were received");
      }
    }

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
        return  0;
      }


      string GetDynamicCallbackUrl()
      {
        if (config.Callback?.AddRandomNumberToHost == null)
        {
          return config.Callback?.Url;
        }

        var uri = new UriBuilder(config.Callback.Url);
        
        uri.Host += rnd.Next(1, config.Callback.AddRandomNumberToHost.Value+1);
        return uri.ToString();
      }


      var transactions = new TransactionReader(config.Filename, config.TxIndex, config.Limit ?? long.MaxValue);

      var client = new HttpClient();
      if (config.Authorization != null)
      {
        client.DefaultRequestHeaders.Add("Authorization", config.Authorization);
      }


      BitcoindProcess bitcoind = null;
      try
      {

        if (!string.IsNullOrEmpty(config.BitcoindConfig?.TemplateData))
        {

          bitcoind = await StartBitcoindWithTemplateDataAsync(config.BitcoindConfig.TemplateData, config.BitcoindConfig.BitcoindPath, httpClientFactory);
          await EnsureMapiIsConnectedToNodeAsync(config.MapiUrl, config.BitcoindConfig.MapiAdminAuthorization, bitcoind);
        }


        try
        {
          _ = await client.GetStringAsync(config.MapiUrl + "mapi/feeQuote"); // test call
        }
        catch (Exception e)
        {
          throw new Exception($"Can not connect to mAPI {config.MapiUrl}. Check if parameters are correct. Error {e.Message}", e);
        }

        var stats = new Stats();


        // Start web server if required
        IHost webServer = null;
        var cancellationSource = new CancellationTokenSource();
        if (config.Callback?.StartListener == true)
        {
          Console.WriteLine($"Starting web server for url {config.Callback.Url}");
          webServer = CallbackServer.Start(config.Callback.Url, cancellationSource.Token, new CallbackReceived(stats, config.Callback?.Hosts));
        }


        async Task submitThread()
        {
          var batchSize = config.BatchSize;
          var batch = new List<string>();
          while (transactions.TryGetnextTransaction(out var transaction))
          {
            batch.Add(transaction);
            if (batch.Count >= batchSize)
            {
              await SendTransactionsBatch(batch, client, stats, config.MapiUrl+ "mapi/txs", GetDynamicCallbackUrl(), config.Callback?.CallbackToken,
                config.Callback?.CallbackEncryption);
              batch.Clear();
            }

          }
          // Send remaining transactions

          if (batch.Any())
          {
            await SendTransactionsBatch(batch, client, stats, config.MapiUrl + "mapi/txs", GetDynamicCallbackUrl(), config.Callback?.CallbackToken,
              config.Callback?.CallbackEncryption);
            batch.Clear();
          }
        }

        async Task PrintProgress(CancellationToken cancellationToken)
        {
          do
          {
            Console.WriteLine(stats);
            await Task.Delay(1000, cancellationToken);

          } while (!cancellationToken.IsCancellationRequested);

        }


        var tasks = new List<Task>();

        Console.WriteLine($"Starting {config.Threads} concurrent tasks");

        for (int i = 0; i < config.Threads; i++)
        {
          tasks.Add(Task.Run(submitThread));
        }

        var progressTask = Task.Run(() => PrintProgress(cancellationSource.Token), cancellationSource.Token);

        Task.WaitAll(tasks.ToArray());

        stats.StopTiming(); // we are no longer submitting txs. Stop the Stopwatch that is used to calculate submission throughput
        if (config.Callback?.StartListener == true  && bitcoind != null && !string.IsNullOrEmpty(config.Callback?.Url) && stats.OKSubmitted > 0)
        {
          Console.WriteLine("Finished sending transactions. Will generate a block to trigger callbacks");
          await bitcoind.RpcClient.GenerateAsync(1);

          await WaitForCallbacksAsync(config.Callback.IdleTimeoutMS, stats); 
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

        GenerateCsvRow(config, stats);
      }
      finally
      {
        bitcoind?.Dispose();
      }

      return 0;

    }

    static void GenerateCsvRow(SendConfig config, Stats stats)
    {
      StatsCsv statsCsv = new(Domain.Const.MERCHANT_API_VERSION, config.Filename, config.BatchSize, config.Threads, string.IsNullOrEmpty(config.Callback?.Url), stats, config.CsvComment);

      string statsFile = "stats.csv";
      bool fileExists = File.Exists(statsFile);
      using var stream = File.Open(statsFile, FileMode.Append);
      using var writer = new StreamWriter(stream);
      CsvConfiguration conf = new(CultureInfo.InvariantCulture)
      {
        Delimiter = ";",
        DetectColumnCountChanges = true
      };

      using var csv = new CsvWriter(writer, conf);
      if (!fileExists)
      {
        csv.WriteHeader<StatsCsv>();
        csv.NextRecord();
      }
      csv.WriteRecord(statsCsv);
      csv.NextRecord();
      csv.Flush();
    }


    private static void DirectoryCopy(string sourceDirName, string destDirName)
    {
      // Get the subdirectories for the specified directory.
      DirectoryInfo dir = new(sourceDirName);

      if (!dir.Exists)
      {
        throw new DirectoryNotFoundException(
          "Source directory does not exist or could not be found: "
          + sourceDirName);
      }


      var dirs = new Queue<(DirectoryInfo, string)>();
      dirs.Enqueue((dir, destDirName));

      while (dirs.TryDequeue(out var current))
      {
        var (currentSrcDir, currentDestDir) = current;

        Directory.CreateDirectory(currentDestDir);

        foreach (FileInfo file in currentSrcDir.GetFiles())
        {
          file.CopyTo(Path.Combine(currentDestDir, file.Name), false);
        }

        // Enqueue subdirectories for later
        foreach (var x in currentSrcDir.GetDirectories())
        {
          dirs.Enqueue((x, Path.Combine(currentDestDir, x.Name)));
        }
      }
    }

    static string CopyTemplateData(string templateData)
    {
      if (!Directory.Exists(Path.Combine(templateData, "blocks")))
      {
        throw new Exception("Invalid templatePath - Directory pointed to by templatePath should contain 'blocks' sub-directory.");
      }

      string testDataDir = Path.Combine(Directory.GetCurrentDirectory(), "nodes", DateTime.Now.ToString("s").Replace(":", "-"));

      string destDir = Path.Combine(testDataDir, "regtest");
      Console.WriteLine($"Copying template data from directory {templateData} into temporary directory {destDir}");
      
      DirectoryCopy(templateData, destDir);
      return testDataDir;
    }


    static async Task EnsureMapiIsConnectedToNodeAsync(string mapiUrl, string authAdmin, BitcoindProcess bitcoind)
    {
      var adminClient = new HttpClient();
      adminClient.DefaultRequestHeaders.Add("Api-Key", authAdmin);
      mapiUrl += "api/v1/Node";

      var uri = new Uri(mapiUrl);
      var hostPort = bitcoind.Host + ":" + bitcoind.RpcPort;

      var nodesResult = await adminClient.GetAsync(mapiUrl);

      if (!nodesResult.IsSuccessStatusCode)
      {
        throw new Exception(
          $"Unable to retrieve existing node {hostPort}. Error: {nodesResult.StatusCode} {await nodesResult.Content.ReadAsStringAsync()}");
      }

      var nodes =
        HelperTools.JSONDeserialize<NodeViewModelGet[]>(await nodesResult.Content.ReadAsStringAsync());
      if (nodes.Any(x => string.Compare(x.Id, hostPort, StringComparison.InvariantCultureIgnoreCase) == 0))
      {
        Console.WriteLine($"Removing existing node {hostPort} from mAPI");

        var deleteResult = await adminClient.DeleteAsync(uri + "/" + hostPort);
        if (!deleteResult.IsSuccessStatusCode)
        {
          throw new Exception(
            $"Unable to delete existing node {hostPort}. Error: {deleteResult.StatusCode} {await deleteResult.Content.ReadAsStringAsync()}");
        }
      }

      Console.WriteLine($"Adding new node {hostPort} to mAPI");

      var newNode = new NodeViewModelCreate
      {
        Id = hostPort,
        Username = bitcoind.RpcUser,
        Password = bitcoind.RpcPassword,
        Remarks = "Node created by mAPI Stress Test at " + DateTime.Now
      };

      var newNodeContent = new StringContent(HelperTools.JSONSerialize(newNode, true),
        new UTF8Encoding(false), MediaTypeNames.Application.Json);

      var newNodeResult = await adminClient.PostAsync(uri, newNodeContent);

      if (!newNodeResult.IsSuccessStatusCode)
      {
        throw new Exception(
          $"Unable to create new {hostPort}. Error: {newNodeResult.StatusCode} {await newNodeResult.Content.ReadAsStringAsync()}");
      }

      await Task.Delay(TimeSpan.FromSeconds(1)); // Give mAPI some time to establish ZMQ subscriptions
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
          description: "Config file containing configuration"
        )
        {
          Arity = new ArgumentArity(1,1)
        }
      };

      sendCommand.Description = "Reads transactions from a file and submit it to mAPI";


      var rootCommand = new RootCommand
      {
        sendCommand
      };

      rootCommand.Description = "mAPI stress test";

      sendCommand.Handler = CommandHandler.Create( async (string configFileName) =>
        await SendTransactions(configFileName, (IHttpClientFactory)host.Services.GetService(typeof(IHttpClientFactory))));

      return await rootCommand.InvokeAsync(args);
    }

    
  }

}
