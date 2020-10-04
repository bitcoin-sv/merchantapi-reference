// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Binding;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MerchantAPI.APIGateway.Rest.ViewModels;
using MerchantAPI.APIGateway.Test.Functional;
using MerchantAPI.APIGateway.Test.Functional.CallBackWebServer;
using MerchantAPI.Common.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
        query.Add("defaultCallBackUrl=" + WebUtility.UrlEncode(callbackUrl));

        if (!string.IsNullOrEmpty(callbackToken))
        {
          query.Add("defaultCallBackToken=" + WebUtility.UrlEncode(callbackToken));
        }

        if (!string.IsNullOrEmpty(callbackEncryption))
        {
          query.Add("defaultCallBackEncryption=" + WebUtility.UrlEncode(callbackEncryption));
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
        ub.Query = ub.Query.Substring(1) + "&" + queryString; // remove leading ?  it is added back automatically
      }

      string urlWithParams = ub.Uri.ToString();


      // We currently submit through REST interface., We could also use binary  interface
      var request = transactions.Select(t => new SubmitTransactionViewModel
      {
        RawTx = t,
        // All other parameters are passed in query string
        CallBackUrl = null, 
        CallBackToken = null,
        CallBackEncryption = null,
        MerkleProof = null,
        DsCheck = null
      }).ToArray();

      var requestString = HelperTools.JSONSerializeNewtonsoft(request, false);
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
        var rEnvelope = HelperTools.JSONDeserializeNewtonsoft<JsonEnvelope>(responseAsString);
        var r = HelperTools.JSONDeserializeNewtonsoft<SubmitTransactionsResponseViewModel>(rEnvelope.Payload);
        int printLimit = 10;
        var errors = r.Txs.Where(t => t.ReturnResult != "success")
          .Select(t => t.Txid + " " + t.ReturnResult + " " + t.ResultDescription).ToArray();

        stats.AddRequestTxFailures(errors.Length);
        stats.AddOkSubmited(request.Length - errors.Length);
        var limitedErrors = string.Join("'", errors.Take(printLimit));
        if (errors.Any())
        {
          Console.WriteLine($"Error while submitting transactions. Printing  up to {printLimit} out of {errors.Length} errors : {limitedErrors}");
        }
      }

    }

    static async Task<BitcoindProcess> StartBitcoindWithTemplateDataAsync(string templateData, string bitcoindPath)
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
        new NullLoggerFactory(), emptyDataDir: false);

      long blocks = (await bitcoind.RpcClient.GetBlockchainInfoAsync()).Blocks;

      if (blocks == 0)
      {
        Console.WriteLine($"Warning: current active chain has 0 blocks. The submission of all transactions will probably fail. Check the content of template data directory - {templateData}");
      }

      return bitcoind;

    }
    static async Task WaitForCallBacksAsync(int timeoutMS, Stats stats)
    {

      // Wait until all callback are received or until timeout expires
      const int timeoutMs = 30_000;
      long callBacksReceived;
      long OKSubmitted;
      do
      {
        await Task.Delay(100);
        callBacksReceived = stats.CallBacksReceived;
        OKSubmitted = stats.OKSubmitted;

      } while (callBacksReceived != OKSubmitted && stats.LastUpdateAgeMs < timeoutMs);

      // NOTE: we have a slight race conditions here if new callbacks are received 
      if (callBacksReceived != OKSubmitted)
      {
        Console.WriteLine($"Error: expected to receive {OKSubmitted} callbacks but received {callBacksReceived} ");
      }
      else
      {
        Console.WriteLine("Ok, all callbacks were received");
      }
    }

    static async Task<int> SendTransactions(string fileName, string url, int batchSize, int txIndex, int threads, string auth, string callbackUrl, string callbackToken, string callbackEncryption, long? limit, 
      bool startListener, string templateData, string authAdmin, string bitcoindPath)
    {

      if (string.IsNullOrEmpty(callbackUrl) && (!string.IsNullOrEmpty(callbackEncryption) || !string.IsNullOrEmpty(callbackToken)))
      {
        throw new Exception($"{nameof(callbackUrl)} is required when either {nameof(callbackEncryption)} or {nameof(callbackToken)}");
      }

      if (startListener && string.IsNullOrEmpty(callbackUrl))
      {
        throw new Exception($"{nameof(callbackUrl)} is required when {nameof(startListener)} is specified");
      }

      if (string.IsNullOrEmpty(authAdmin) ^ string.IsNullOrEmpty(templateData))
      {
        throw new Exception($"{nameof(authAdmin)} must be specified if and only if {nameof(templateData)} is specified.");
      }



      var transactions = new TransactionReader(fileName, txIndex, limit ?? long.MaxValue);

      var client = new HttpClient();
      if (auth != null)
      {
        client.DefaultRequestHeaders.Add("Authorization", auth);
      }


      BitcoindProcess bitcoind = null;
      try
      {

        if (!string.IsNullOrEmpty(templateData))
        {

          bitcoind = await StartBitcoindWithTemplateDataAsync(templateData, bitcoindPath);
          await EnsureMapiIsConnectedToNodeAsync(url, authAdmin, bitcoind);
        }


        try
        {
          _ = await client.GetStringAsync(url + "mapi/feeQuote"); // test call
        }
        catch (Exception e)
        {
          throw new Exception($"Can not connect to mAPI {url}. Check if parameters are correct. Error {e.Message}", e);
        }

        var stats = new Stats();


        // Start web server if required
        IHost webServer = null;
        var cancellationSource = new CancellationTokenSource();
        if (startListener)
        {
          Console.WriteLine($"Starting web server for url {callbackUrl}");
          webServer = StartWebServer.Start(callbackUrl, cancellationSource.Token, new CallBackReceived(stats));
        }


        async Task submitThread()
        {
          var batch = new List<string>(batchSize);
          while (transactions.TryGetnextTransaction(out var transaction))
          {
            batch.Add(transaction);
            if (batch.Count >= batchSize)
            {
              await SendTransactionsBatch(batch, client, stats, url + "mapi/txs", callbackUrl, callbackToken,
                callbackEncryption);
              batch.Clear();
            }

          }
          // Send remaining transactions

          if (batch.Any())
          {
            await SendTransactionsBatch(batch, client, stats, url + "mapi/txs", callbackUrl, callbackToken,
              callbackEncryption);
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

        Console.WriteLine($"Starting {threads} concurrent tasks");

        for (int i = 0; i < threads; i++)
        {
          tasks.Add(Task.Run(submitThread));
        }

        var progressTask = Task.Run(() => PrintProgress(cancellationSource.Token), cancellationSource.Token);

        Task.WaitAll(tasks.ToArray());

        stats.StopTiming(); // we are no longer submitting txs. Stop the Stopwatch that is used to calculate submission trhoughput
        if (startListener && bitcoind != null && !string.IsNullOrEmpty(callbackUrl) && stats.OKSubmitted > 0)
        {
          Console.WriteLine("Finished sending transactions. Will generate a block to trigger callbacks");
          await bitcoind.RpcClient.GenerateAsync(1);
          await WaitForCallBacksAsync(30_000, stats);
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


      }
      finally
      {
        bitcoind?.Dispose();
      }

      return 0;

    }


    private static void DirectoryCopy(string sourceDirName, string destDirName)
    {
      // Get the subdirectories for the specified directory.
      DirectoryInfo dir = new DirectoryInfo(sourceDirName);

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
        HelperTools.JSONDeserializeNewtonsoft<NodeViewModelGet[]>(await nodesResult.Content.ReadAsStringAsync());
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

      var newNodeContent = new StringContent(HelperTools.JSONSerializeNewtonsoft(newNode, true),
        new UTF8Encoding(false), MediaTypeNames.Application.Json);

      var newNodeResult = await adminClient.PostAsync(uri, newNodeContent);

      if (!newNodeResult.IsSuccessStatusCode)
      {
        throw new Exception(
          $"Unable to create new {hostPort}. Error: {newNodeResult.StatusCode} {await newNodeResult.Content.ReadAsStringAsync()}");
      }

    }

    static async Task<int> Main(string[] args)
    {


      var sendCommand = new Command("send")
      {
        new Option<string>(
          new[] {"--filename", "-f"},
          description: "File containing transactions"
        )
        {
          IsRequired = true
        },

        new Option<string>(
          new[] {"--url", "-u"},
          description: "URL used for submitting transactions. Example: http://localhost:5000/"
        )
        {
          IsRequired = true
        },


        new Option<string>(
          new[] {"--callbackUrl", "-cu"},
          description: "Url that will process double spend and merkle proof notifications. When present, transactions will be submitted with MerkleProof and DsCheck set to true. Required when any other callback parameter is supplied. Example: http://localhost:2000/callbacks"
        )
        {
          IsRequired = false
        },

        new Option<string>(
          new[] {"--callbackToken", "-ct"},
          description: "Full authorization header used when performing callbacks."
        )
        {
          IsRequired = false
        },

        new Option<string>(
          new[] {"--callbackEncryption", "-ce"},
          description: "Encryption parameters used when performing callbacks."
        )
        {
          IsRequired = false
        },

        new Option<bool>(
          new[] {"--startListener", "-s"},
          description: "Start a listener that will listen to callbacks on port specified by --callBackUrl"
        )
        {
          IsRequired = false
        },


        new Option<int>(
          new[] {"--batchSize", "-b"},
          description: "Number of transactions submitted in one call",
          getDefaultValue: () => 100
        )
        {
          IsRequired = false
        },


        new Option<string>(
          new[] {"--txIndex", "-i"},
          description: "Specifies a zero based index of the transaction in case of semi colon separated files"
        )
        {
          IsRequired = false
        },
        new Option<int>(
          new[] {"--threads", "-t"},
          description: "Number of concurrent threads that will be used to submit the transactions. When using multiple threads, make sure that transactions in the file are not dependent on each other",
          getDefaultValue: () => 1
        )
        {
          IsRequired = false
        },

        new Option<int>(
          new[] {"--auth", "-a"},
          description: "Authorization header used when submitting transactions"
        )
        {
          IsRequired = false
        },

        new Option<long>(
          new[] {"--limit", "-l"},
          description: "Only submit up to specified number of transactions"
        )
        {
          IsRequired = false
        },

        new Option<string>(
          new[] {"--templateData", "-T"},
          description: "Template directory containing snapshot if data directory that will be used as initial state of new node that is started up. If specified --authAdmin must also be specified."
        )
        {
          IsRequired = false
        },

        new Option<string>(
          new[] {"--authAdmin", "-A"},
          description: "Authentication used when adding newly started node to mAPI. Can only be used with --templateData "
        )
        {
          IsRequired = false
        },

        new Option<string>(
          new[] {"--bitcoindPath", "-bp"},
          description: "Full path to bitcoind executable. Used when starting new node if --templateData is specified. If not specified, bitcoind executable must be in current directory. Example :/usr/bitcoin/bircoind "
        )
        {
          IsRequired = false
        },


      };

      sendCommand.Description = "Reads transactions from a file and submit it to mAPI";


      var rootCommand = new RootCommand
      {
        sendCommand
      };

      rootCommand.Description = "mAPI stress test";

      sendCommand.Handler = CommandHandlerCreate(
        (string fileName, string url, int batchSize, int txIndex, int threads, string auth, string callbackUrl, string callbackToken, string callbackEncryption, long? limit, bool startListener, string templateData, string authAdmin, string bitcoindPath) =>
        SendTransactions(fileName, url, batchSize, txIndex, threads, auth, callbackUrl, callbackToken, callbackEncryption, limit, startListener, templateData, authAdmin, bitcoindPath).Result);

      return await rootCommand.InvokeAsync(args);
    }

    // Helper method. System.CommandLine defines overloads just up to T7
    static ICommandHandler CommandHandlerCreate<T1, T2, T3, T4, T5, T6, T7, T8, T9,T10,T11, T12, T13, T14>(
      Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, int> action) =>
      HandlerDescriptor.FromDelegate(action).GetCommandHandler();

  }

}
