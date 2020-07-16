// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Binding;
using System.CommandLine.Invocation;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MerchantAPI.APIGateway.Rest.ViewModels;
using MerchantAPI.Common.Json;

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

    static async Task<int> SendTransactions(string fileName, string url, int batchSize, int txIndex, int threads, string auth, string callbackUrl, string callbackToken, string callbackEncryption, long? limit)
    {

      if (string.IsNullOrEmpty(callbackUrl) && (!string.IsNullOrEmpty(callbackEncryption) || !string.IsNullOrEmpty(callbackToken)))
      {
        throw new Exception($"{nameof(callbackUrl)} is required when either {nameof(callbackEncryption)} or {nameof(callbackToken)}");
      }

      var transactions = new TransactionReader(fileName, txIndex, limit ?? long.MaxValue);

      var client = new HttpClient();
      if (auth != null)
      {
        client.DefaultRequestHeaders.Add("Authorization", auth);
      }

      try
      {
        _ = await client.GetStringAsync(url + "mapi/feeQuote"); // test call
      }
      catch (Exception e)
      {
        throw new Exception($"Can not connect to mAPI {url}. Check if parameters are correct. Error {e.Message}", e);
        throw;
      }
      


      var stats = new Stats();

      async Task submitThread()
      {
        var batch = new List<string>(batchSize);
        while (transactions.TryGetnextTransaction(out var transaction))
        {
          batch.Add(transaction);
          if (batch.Count >= batchSize)
          {
            await SendTransactionsBatch(batch, client, stats, url + "mapi/txs", callbackUrl, callbackToken, callbackEncryption);
            batch.Clear();
          }
          
        }
        // Send remaining transactions

        if (batch.Any())
        {
          await SendTransactionsBatch(batch, client, stats, url + "mapi/txs", callbackUrl, callbackToken, callbackEncryption);
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

      var source = new CancellationTokenSource();
      var progressTask  = Task.Run(() => PrintProgress(source.Token), source.Token);

      Task.WaitAll(tasks.ToArray());

      source.Cancel(false);

      try
      {
        progressTask.Wait();
      }
      catch (OperationCanceledException)
      {
      }

      return 0;

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
      };

      sendCommand.Description = "Reads transactions from a file and submit it to mAPI";


      var rootCommand = new RootCommand
      {
        sendCommand
      };

      rootCommand.Description = "mAPI stress test";

      sendCommand.Handler = CommandHandlerCreate(
        (string fileName, string url, int batchSize, int txIndex, int threads, string auth, string callbackUrl, string callbackToken, string callbackEncryption, long? limit) =>
        SendTransactions(fileName, url, batchSize, txIndex, threads, auth, callbackUrl, callbackToken, callbackEncryption, limit).Result);

      return await rootCommand.InvokeAsync(args);
    }

    // Helper method. System.CommandLine defines overloads just up to T7
    static ICommandHandler CommandHandlerCreate<T1, T2, T3, T4, T5, T6, T7, T8, T9,T10>(
      Func<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, int> action) =>
      HandlerDescriptor.FromDelegate(action).GetCommandHandler();

  }
}
