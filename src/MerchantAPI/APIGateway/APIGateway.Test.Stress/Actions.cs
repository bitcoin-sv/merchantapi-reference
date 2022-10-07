// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using Dapper;
using MerchantAPI.APIGateway.Domain;
using MerchantAPI.APIGateway.Domain.Models;
using MerchantAPI.APIGateway.Domain.ViewModels;
using MerchantAPI.APIGateway.Infrastructure.Repositories;
using MerchantAPI.APIGateway.Rest.ViewModels;
using MerchantAPI.Common.Json;
using MerchantAPI.Common.Tasks;
using NBitcoin;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Test.Stress
{
  internal class Actions
  {
    public static async Task SendTransactionsBatch(IEnumerable<string> transactions, HttpClient client, Stats stats, string url, string callbackUrl, string callbackToken, string callbackEncryption)
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
        ub.Query = ub.Query + "&" + queryString; // see https://docs.microsoft.com/en-us/dotnet/api/system.uribuilder.query
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

        stats.AddRequestTxFailures(callbackHost, errorItems.Select(x => new uint256(x.Txid)));
        stats.AddOkSubmited(callbackHost, okItems.Select(x => new uint256(x.Txid)), okItems.Where(x => x.ResultDescription == "Already known").Count());

        var errors = errorItems
          .Select(t => t.Txid + " " + t.ReturnResult + " " + t.ResultDescription).ToArray();



        var limitedErrors = string.Join(Environment.NewLine, errors.Take(printLimit));
        if (errors.Any())
        {
          Console.WriteLine($"Error while submitting transactions. Printing  up to {printLimit} out of {errors.Length} errors : {limitedErrors}");
        }
      }
    }

    /// <summary>
    /// Wait until all callback are received or until timeout expires
    /// Print out any missing callbacks
    /// </summary>
    public static async Task WaitForCallbacksAsync(int timeoutMs, Stats stats)
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
        Console.WriteLine($"Error: Not all callbacks were received. Total missing {missing.Sum(x => x.txs.Length)}");
        Console.WriteLine($"Printing up to {printUpTo} missing tx per host");
        foreach (var host in missing)
        {
          Console.WriteLine($"   {host.host}  {string.Join(" ", host.txs.Take(printUpTo).Select(x => x.ToString()).ToArray())} ");
        }
      }
      else
      {
        Console.WriteLine("Ok, all callbacks were received");
      }
    }

    public static async Task<long> GetAllSuccessfulTransactionsAsync(string mapiDBConnectionString)
    {
      using var connection = new NpgsqlConnection(mapiDBConnectionString);
      RetryUtils.Exec(() => connection.Open());

      string cmdText = @$"SELECT COUNT(*) FROM Tx WHERE txstatus = {TxStatus.Accepted}";

      var txsCount = await connection.QuerySingleAsync<long>(cmdText);

      return txsCount;
    }

    public static async Task<(Tx[] missingTxs, TimeSpan elapsedTime)> GetMissingTransactionsAsync(string mapiDBConnectionString, string[] mempoolTxs)
    {
      using var connection = new NpgsqlConnection(mapiDBConnectionString);
      RetryUtils.Exec(() => connection.Open());

      Console.WriteLine($"There is {mempoolTxs.Length} transactions present in mempool.");
      var watch = Stopwatch.StartNew();
      var txs = await TxRepositoryPostgres.GetMissingTransactionsAsync(connection, mempoolTxs, DateTime.MaxValue);
      watch.Stop();
      Console.WriteLine($"Number of transactions, that are missing in mempool: {txs.Length}. DB query took: {watch.Elapsed}.");
      return (txs, watch.Elapsed);
    }

    public static async Task CleanUpTxHandler(string mapiDBConnectionStringDDL)
    {
      using var connection = new NpgsqlConnection(mapiDBConnectionStringDDL);
      RetryUtils.Exec(() => connection.Open());
      var watch = Stopwatch.StartNew();

      (int blocks, long txs, int mempoolTxs) = await TxRepositoryPostgres.CleanUpTxAsync(connection, DateTime.MaxValue, DateTime.MaxValue, null);

      watch.Stop();
      Console.WriteLine($"CleanUpTxHandler: deleted {blocks} blocks, {txs} txs, {mempoolTxs} mempool/blockchain txs. Elapsed: {watch.Elapsed}.");
    }

    public static void PrintToConsoleWithColor(string text, ConsoleColor color)
    {
      Console.ForegroundColor = color;
      Console.WriteLine(text);
      Console.ResetColor();
    }
  }
}
