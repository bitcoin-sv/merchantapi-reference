﻿// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MerchantAPI.Common.BitcoinRpc;
using MerchantAPI.Common.BitcoinRpc.Responses;
using MerchantAPI.Common.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NBitcoin.Crypto;
using Prometheus;
using MerchantAPI.APIGateway.Domain.Metrics;

namespace MerchantAPI.APIGateway.Domain.Models
{

  /// <summary>
  /// Handle calls to multiple nodes. Implement different strategies for different RPC calls
  ///    -sendtransactions- submit to all nodes and merge results
  ///   - getTransaction - checks if all successful responses are the same
  ///   - getBlockchainInfoAsync() - oldest block
  ///   - ...
  /// </summary>
  public class RpcMultiClient : IRpcMultiClient
  {
    readonly INodes nodes;
    readonly IRpcClientFactory rpcClientFactory;
    readonly ILogger logger;
    readonly RpcClientSettings rpcClientSettings;
    readonly RpcMultiClientMetrics rpcMultiClientMetrics;

    public RpcMultiClient(INodes nodes, IRpcClientFactory rpcClientFactory, ILogger<RpcMultiClient> logger, IOptions<AppSettings> options, RpcMultiClientMetrics rpcMultiClientMetrics)
      : this(nodes, rpcClientFactory, logger, options.Value.RpcClient, rpcMultiClientMetrics)
    {
    }

    public RpcMultiClient(INodes nodes, IRpcClientFactory rpcClientFactory, ILogger<RpcMultiClient> logger, RpcClientSettings rpcClientSettings, RpcMultiClientMetrics rpcMultiClientMetrics)
    {
      this.nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
      this.rpcClientFactory = rpcClientFactory ?? throw new ArgumentNullException(nameof(rpcClientFactory));
      this.logger = logger;
      this.rpcClientSettings = rpcClientSettings;
      this.rpcMultiClientMetrics = rpcMultiClientMetrics ?? throw new ArgumentNullException(nameof(rpcMultiClientMetrics));
    }

    static void ShuffleArray<T>(T[] array)
    {
      int n = array.Length;
      while (n > 1)
      {
        int i = System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, n--);
        T temp = array[n];
        array[n] = array[i];
        array[i] = temp;
      }
    }

    public IRpcClient[] GetRpcClients()
    {
      var result = nodes.GetNodes().Select(
        n => rpcClientFactory.Create(
          n.Host,
          n.Port,
          n.Username,
          n.Password,
          rpcClientSettings.RequestTimeoutSec.Value,
          rpcClientSettings.MultiRequestTimeoutSec.Value,
          rpcClientSettings.NumOfRetries.Value,
          rpcClientSettings.WaitBetweenRetriesMs.Value)).ToArray();

      if (!result.Any())
      {
        throw new ServiceUnavailableException("No nodes available");
      }

      return result;

    }

    async Task<T> GetFirstSuccessfulAsync<T>(Func<IRpcClient, Task<T>> call)
    {
      Exception lastError = null;
      var rpcClients = GetRpcClients();
      ShuffleArray(rpcClients);
      foreach (var rpcClient in rpcClients)
      {
        try
        {
          return await call(rpcClient);
        }
        catch (Exception e)
        {
          lastError = e;
          logger.LogError($"Error while calling node {rpcClient}. {e.Message} ");
          // try with the next node
        }
      }
      throw lastError ?? new ServiceUnavailableException("No nodes available");
    }

    async Task<Task<T>[]> GetAll<T>(Func<IRpcClient, Task<T>> call)
    {
      var rpcClients = GetRpcClients();

      Task<T>[] tasks = rpcClients.Select(x =>
        {
          try
          {
            return call(x);
          }
          catch (Exception e)
          {
            // Catch exceptions that can happen if call is implemented synchronously
            return Task.FromException<T>(e);
          }
        }

      ).ToArray();
      try
      {
        await Task.WhenAll(tasks);
      }
      catch (Exception)
      {
        // We aren't logging exceptions here because caller methods must handle logging
      }

      return tasks;

    }

    async Task<T[]> GetAllWithoutErrors<T>(Func<IRpcClient, Task<T>> call, bool throwIfEmpty = true)
    {
      var tasks = await GetAll(call);

      var successful = tasks.Where(t => t.IsCompletedSuccessfully).Select(t => t.Result).ToArray();

      if (throwIfEmpty && !successful.Any())
      {
        var firstException = ExtractFirstException(tasks);
        if (firstException.GetBaseException() is RpcException)
        {
          throw new DomainException($"None of the nodes returned successful response.", new Exception($"First error: {firstException}"));
        }
        throw new ServiceUnavailableException("Failed to connect to node(s).", new Exception($"First error: {firstException}"));
      }

      return successful;
    }

    private static AggregateException ExtractFirstException<T>(Task<T>[] tasks)
    {
      // Try to extract exception, preferring RpcExceptions
      return tasks.FirstOrDefault(t => t.Exception?.GetBaseException() is RpcException)?.Exception
        ?? tasks.FirstOrDefault(t => t.Exception != null)?.Exception;
    }

    /// <summary>
    /// Calls all node, return one of the following:
    ///  result - first successful result
    ///  allOkTheSame - if all successful results contains the same response
    ///  error - first error 
    /// JsonSerializer is used to check of responses are the same
    /// </summary>
    async Task<(T firstOkResult, bool allOkTheSame, Exception firstError)> GetAllSuccessfulCheckTheSame<T>(Func<IRpcClient, Task<T>> call)
    {
      var tasks = await GetAll(call);

      var successful = tasks.Where(t => t.IsCompletedSuccessfully).Select(t => t.Result).ToArray();
     
      var firstException = ExtractFirstException(tasks);

      if (firstException != null && !successful.Any()) // return error if there are no successful responses
      {
        if (firstException.GetBaseException() is RpcException)
        {
          return (default, true, firstException);
        }
        throw new ServiceUnavailableException("Failed to connect to node(s).", new Exception($"First error: {firstException}"));
      }

      if (successful.Length > 1)
      {
        var firstSuccesfullJson = JsonSerializer.Serialize(successful.First());
        if (successful.Skip(1).Any(x => JsonSerializer.Serialize(x) != firstSuccesfullJson))
        {
          return (default, false, firstException);
        }
      }

      return (successful.First(), true, firstException);
    }

    async Task<(T firstOkResult, bool allOkTheSame, Exception firstError)> GetAllSuccessfulCompareTheSame<T>(Func<IRpcClient, Task<T>> call)
    {
      var tasks = await GetAll(call);

      var successful = tasks.Where(t => t.IsCompletedSuccessfully).Select(t => t.Result).ToArray();

      var firstException = ExtractFirstException(tasks);

      if (firstException != null && !successful.Any()) // return error if there are no successful responses
      {
        if (firstException.GetBaseException() is RpcException)
        {
          return (default, true, firstException);
        }
        throw new ServiceUnavailableException("Failed to connect to node(s).", new Exception($"First error: {firstException}"));
      }

      if (successful.Length > 1)
      {
        var first = successful.First();
        if (successful.Skip(1).Any(x => Comparer<T>.Default.Compare(successful.First(), x) != 0))
        {
          return (default, false, firstException);
        }
      }

      return (successful.First(), true, firstException);
    }

    public Task<byte[]> GetRawTransactionAsBytesAsync(string txId)
    {
      return GetFirstSuccessfulAsync(c => c.GetRawTransactionAsBytesAsync(txId));
    }
    public async Task<RpcGetBlockchainInfo> GetBestBlockchainInfoAsync()
    {
      var r = await GetBlockchainInfoAsync();
      // Sort the results with the highest block height first
      return r.OrderByDescending(x => x.Blocks).FirstOrDefault();
    }

    public async Task<RpcGetBlockchainInfo> GetWorstBlockchainInfoAsync()
    {
      var r = await GetBlockchainInfoAsync();
      // Sort the results with the lowest block height first
      return r.OrderBy(x => x.Blocks).FirstOrDefault();
    }

    private async Task<RpcGetBlockchainInfo[]> GetBlockchainInfoAsync()
    {
      var r = await GetAllWithoutErrors(c => c.GetBlockchainInfoAsync());

      return r;
    }

    public Task<RpcGetMerkleProof> GetMerkleProofAsync(string txId, string blockHash)
    {
      return GetFirstSuccessfulAsync(x => x.GetMerkleProofAsync(txId, blockHash));
    }

    public Task<RpcGetMerkleProof2> GetMerkleProof2Async(string blockHash, string txId)
    {
      return GetFirstSuccessfulAsync(x => x.GetMerkleProof2Async(blockHash, txId));
    }

    public Task<RpcBitcoinStreamReader> GetBlockAsStreamAsync(string blockHash, CancellationToken? token = null)
    {
      return GetFirstSuccessfulAsync(x => x.GetBlockAsStreamAsync(blockHash, token));
    }

    public Task<RpcGetBlockHeader> GetBlockHeaderAsync(string blockHash)
    {
      return GetFirstSuccessfulAsync(x => x.GetBlockHeaderAsync(blockHash));
    }


    public Task<(RpcGetRawTransaction firstOkResult, bool allOkTheSame, Exception firstError)> GetRawTransactionAsync(string id)
    {
      return GetAllSuccessfulCheckTheSame(c => c.GetRawTransactionAsync(id));
    }

    public Task<RpcGetNetworkInfo> GetAnyNetworkInfoAsync()
    {
      return GetFirstSuccessfulAsync(c => c.GetNetworkInfoAsync(retry: false));
    }

    public Task<RpcGetTxOuts> GetTxOutsAsync(IEnumerable<(string txId, long N)> outpoints, string[] fieldList)
    {
      using (rpcMultiClientMetrics.GetTxOutsDuration.NewTimer())
      {
        return GetFirstSuccessfulAsync(c => c.GetTxOutsAsync(outpoints, fieldList));
      }
    }

    public Task<(RpcGetTxOuts firstOkResult, bool allOkTheSame, Exception firstError)> GetTxOutsAsync(IEnumerable<(string txId, long N)> outpoints, string[] fieldList, bool includeMempool)
    {
      return GetAllSuccessfulCompareTheSame(c => c.GetTxOutsAsync(outpoints, fieldList, includeMempool));
    }

    public Task<RpcVerifyScriptResponse[]> VerifyScriptAsync(bool stopOnFirstInvalid,
                                        int totalTimeoutSec,
                                        IEnumerable<(string Tx, int N)> dsTx)
    {
      return GetFirstSuccessfulAsync(c => c.VerifyScriptAsync(stopOnFirstInvalid, totalTimeoutSec, dsTx));
    }

    public Task<string[]> GetRawMempool(CancellationToken? token = null)
    {
      return GetFirstSuccessfulAsync(x => x.GetRawMempool(token));
    }

    public Task<RpcGetMempoolAncestors> GetMempoolAncestors(string txId, CancellationToken? token = null)
    {
      return GetFirstSuccessfulAsync(x => x.GetMempoolAncestors(txId, token));
    }

    enum GroupType
    {
      OK,
      Known,
      Evicted,
      FailureRetryable,
      Invalid
      // order is important - when doing changes check ChooseNewValue
    }

    class ResponseCollidedTransaction
    {
      public string Txid { get; set; }
      public long Size { get; set; }
      public string Hex { get; set; }
    }

    class ResponseTransactionType
    {
      public GroupType Type { get; set; }
      public int? RejectCode { get; set; }
      public string RejectReason { get; set; }
      public ResponseCollidedTransaction[] CollidedWith { get; set; }
      public UnconfirmedAncestor[] UnconfirmedAncestors { get; set; }
    }

    class UnconfirmedAncestor
    {
      public string Txid { get; set; }

      public UnconfirmedAncestorVin[] Vin { get; set; }
    }

    class UnconfirmedAncestorVin
    {
      public string Txid { get; set; }

      public int Vout { get; set; }
    }

    static Dictionary<string, ResponseTransactionType> CategorizeTransactions(
      RpcSendTransactions rpcResponse, string[] submittedTxids)
    {
      var processed =
        new Dictionary<string, ResponseTransactionType>(
          StringComparer.InvariantCulture);

      if (rpcResponse.Invalid != null)
      {
        foreach (var invalid in rpcResponse.Invalid)
        {
          GroupType type;
          if (invalid.RejectCode.HasValue && NodeRejectCode.MapiSuccessCodes.Contains(invalid.RejectCode.Value))
          {
            type = GroupType.Known;
          }
          else
          {
            var rejectCodeAndReason = NodeRejectCode.CombineRejectCodeAndReason(invalid.RejectCode, invalid.RejectReason);
            type = NodeRejectCode.MapiRetryCodesAndReasons.Any(x => rejectCodeAndReason.StartsWith(x)) ? GroupType.FailureRetryable : GroupType.Invalid;
          }
          processed.TryAdd(
            invalid.Txid, 
            new ResponseTransactionType
            {
              Type = type,
              RejectCode = invalid.RejectCode, 
              RejectReason = invalid.RejectReason,
              CollidedWith = invalid.CollidedWith?.Select(t => 
                new ResponseCollidedTransaction 
                { 
                  Txid = t.Txid, 
                  Size = t.Size, 
                  Hex = t.Hex
                }
              ).ToArray()
            }
          );
        }
      }

      if (rpcResponse.Evicted != null)
      {
        foreach (var evicted in rpcResponse.Evicted)
        {
          processed.TryAdd(
            evicted,
            new ResponseTransactionType
            {
              Type = GroupType.Evicted,
              RejectCode = null,
              RejectReason = null
            }
          );
        }
      }

      if (rpcResponse.Known != null)
      {
        foreach (var known in rpcResponse.Known)
        {
          processed.TryAdd(
            known,
            new ResponseTransactionType
            {
              Type = GroupType.Known,
              RejectCode = null,
              RejectReason = null
            }
          );
        }
      }

      foreach (var ok in submittedTxids.Except(processed.Keys, StringComparer.InvariantCulture))
      {
        processed.Add(
          ok,
          new ResponseTransactionType
          {
            Type = GroupType.OK,
            RejectCode = null,
            RejectReason = null,
            UnconfirmedAncestors = rpcResponse.Unconfirmed?.FirstOrDefault(x => x.Txid == ok)?.Ancestors.Select(y => 
              new UnconfirmedAncestor() 
              { 
                Txid = y.Txid, 
                Vin = y.Vin.Select(i => 
                new UnconfirmedAncestorVin()
                {
                  Txid = i.Txid,
                  Vout = i.Vout
                }).ToArray()
              }
            ).ToArray()
          }
        );
      }

      return processed;
    }

    static ResponseTransactionType ChooseNewValue(
      ResponseTransactionType oldValue,
      ResponseTransactionType newValue)
    {
      if (newValue.Type != oldValue.Type)
      {
        // GroupType: OK < Known < Evicted < FailureRetryable < Invalid
        // user should resubmit evicted txs and when mempool errors occured (FailureRetryable)
        return newValue.Type < oldValue.Type ? newValue : oldValue;
      }
      // In case of different error messages we treat the result as Error
      return oldValue;
    }


    /// <summary>
    /// Updates oldResults with data from newResults
    /// </summary>
    static void AddNewResults(
      Dictionary<string, ResponseTransactionType> oldResults,
      Dictionary<string, ResponseTransactionType> newResults)
    {

      foreach (var n in newResults)
      {
        if (oldResults.TryGetValue(n.Key, out var oldValue))
        {
          oldResults[n.Key] = ChooseNewValue(oldValue, n.Value);
        }
        else
        {
          // This happens when oldResults is empty. It shouln't happen otherwise, since same 
          // transactions was sent to all of the nodes
          oldResults.Add(n.Key, n.Value);
        }
      }
    }

    public async Task<RpcSendTransactions> SendRawTransactionsAsync(
      (byte[] transaction, bool allowhighfees, bool dontCheckFees, bool listUnconfirmedAncestors, Dictionary<string, object> config)[] transactions)
    {
      var allTxs = transactions.Select(x => Hashes.DoubleSHA256(x.transaction).ToString()).ToArray();
      RpcSendTransactions[] okResults = null;

      using (rpcMultiClientMetrics.SendRawTxsDuration.NewTimer())
      {
        okResults = await GetAllWithoutErrors(c => c.SendRawTransactionsAsync(transactions));
      }

      // Extract results from nodes that successfully processed the request and merge them together:

      var results =
        new Dictionary<string, ResponseTransactionType>(
          StringComparer.InvariantCulture);
      foreach (var ok in okResults)
      {
        AddNewResults(results, CategorizeTransactions(ok, allTxs));
      }

      var result = new RpcSendTransactions
      {
        Evicted = results.Where(x => x.Value.Type == GroupType.Evicted)
          .Select(x => x.Key).ToArray(),

        // Treat failureRetryable results as invalid transaction
        Invalid = results.Where(x => x.Value.Type == GroupType.FailureRetryable || x.Value.Type == GroupType.Invalid)
          .Select(x =>
            new RpcSendTransactions.RpcInvalidTx
            {
              Txid = x.Key,
              RejectCode = x.Value.RejectCode,
              RejectReason = x.Value.RejectReason,
              CollidedWith = x.Value.CollidedWith?.Select(t =>
               new RpcSendTransactions.RpcCollisionTx
               {
                 Txid = t.Txid,
                 Size = t.Size,
                 Hex = t.Hex
               }
              ).ToArray()
            })
          .ToArray(),

        Known = results.Where(x => x.Value.Type == GroupType.Known)
          .Select(x => x.Key).ToArray(),
        Unconfirmed = results.Where (x => x.Value.UnconfirmedAncestors != null)
          .Select(x => new RpcSendTransactions.RpcUnconfirmedTx
          {
            Txid = x.Key,
            Ancestors = x.Value.UnconfirmedAncestors.Select(y => 
              new RpcSendTransactions.RpcUnconfirmedAncestor()
              {
                Txid = y.Txid,
                Vin = y.Vin.Select(i => 
                  new RpcSendTransactions.RpcUnconfirmedAncestorVin()
                  {
                    Txid = i.Txid,
                    Vout = i.Vout
                  }
                ).ToArray()
              }
            ).ToArray()
          })
        .ToArray()
      };

      return result;

    }
  }
}
