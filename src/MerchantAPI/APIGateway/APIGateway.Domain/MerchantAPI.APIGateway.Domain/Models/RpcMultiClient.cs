// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using MerchantAPI.Common.BitcoinRpc;
using MerchantAPI.Common.BitcoinRpc.Responses;
using Microsoft.Extensions.Logging;
using NBitcoin.Crypto;

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
    INodes nodes;
    IRpcClientFactory rpcClientFactory;
    private ILogger logger;

    public RpcMultiClient(INodes nodes, IRpcClientFactory rpcClientFactory, ILogger<RpcMultiClient> logger)
    {
      this.nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
      this.rpcClientFactory = rpcClientFactory ?? throw new ArgumentNullException(nameof(rpcClientFactory));
      this.logger = logger;
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

    IRpcClient[] GetRpcClients()
    {
      var result = nodes.GetNodes().Select(
        n => rpcClientFactory.Create(n.Host, n.Port, n.Username, n.Password)).ToArray();

      if (!result.Any())
      {
        throw new Exception("No nodes available"); 
      }

      return result;

    }

    Task<T> GetFirstSucesfullAsync<T>(Func<IRpcClient, Task<T>> call)
    {
      Exception lastError = null;
      var rpcClients = GetRpcClients();
      ShuffleArray(rpcClients);
      foreach (var rpcClient in rpcClients)
      {
        try
        {
          return call(rpcClient);
        }
        catch (Exception e)
        {
          lastError = e;
          logger.LogError($"Error while calling node {rpcClient}. {e} ");
          // try with the next node
        }
      }

      throw lastError ?? new Exception("No nodes available"); 
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
      catch (Exception e)
      {
        logger.LogError(
          $"Error while calling RPC.  Error: {e}");
      }

      return tasks;

    }
    async Task<T[]> GetAllWithoutErrors<T>(Func<IRpcClient, Task<T>> call, bool throwIfEmpty = true)
    {
      var tasks = await GetAll(call);


      var sucesfull = tasks.Where(t => t.IsCompletedSuccessfully).Select(t => t.Result).ToArray();

      if (throwIfEmpty && !tasks.Any())
      {
        throw new Exception($"None of the nodes returned successful response. First error: {tasks[0].Exception} ");
      }

      return sucesfull;
    }

    /// <summary>
    /// Calls all node, return one of the following:
    ///  result - first successful result
    ///  allOkTheSame - if all successful results contains the same response
    ///  error - first error 
    /// JsonSerializer is used to check of responses are the same
    /// </summary>
    async Task<(T firstOkResult, bool allOkTheSame, Exception firstError)> GetAllSucesfullCheckTheSame<T>(Func<IRpcClient, Task<T>> call)
    {
      var tasks = await GetAll(call);


      var sucesfull = tasks.Where(t => t.IsCompletedSuccessfully).Select(t => t.Result).ToArray();

      // Try to extract exception, preferring RpcExceptions 
      var firstException =
        tasks.FirstOrDefault(t => t.Exception?.GetBaseException() is RpcException)?.Exception
        ?? tasks.FirstOrDefault(t => t.Exception != null)?.Exception;

      if (firstException != null && !sucesfull.Any()) // return error if there are no successful responses
      {

        return (default, true, firstException);
      }

      if (sucesfull.Length > 1)
      {
        var firstSuccesfullJson = JsonSerializer.Serialize(sucesfull.First());
        if (sucesfull.Skip(0).Any(x => JsonSerializer.Serialize(x) != firstSuccesfullJson))
        {
          return (default, false, firstException);
        }
      }

      return (sucesfull.First(), true, firstException);
    }

    public Task<byte[]> GetRawTransactionAsBytesAsync(string txId)
    {
      return GetFirstSucesfullAsync(c => c.GetRawTransactionAsBytesAsync(txId));
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

      if (!r.Any())
      {
        throw new Exception("No working nodes are available");
      }
      return r;
    }

    public Task<RpcGetMerkleProof> GetMerkleProofAsync(string txId, string blockHash)
    {
      return GetFirstSucesfullAsync(x => x.GetMerkleProofAsync(txId, blockHash));
    }

    public Task<RpcBitcoinStreamReader> GetBlockAsStreamAsync(string blockHash)
    {
      return GetFirstSucesfullAsync(x => x.GetBlockAsStreamAsync(blockHash));
    }

    public Task<RpcGetBlockHeader> GetBlockHeaderAsync(string blockHash)
    {
      return GetFirstSucesfullAsync(x => x.GetBlockHeaderAsync(blockHash));
    }


    public Task<(RpcGetRawTransaction firstOkResult, bool allOkTheSame, Exception firstError)> GetRawTransactionAsync(string id)
    {
      return GetAllSucesfullCheckTheSame(c => c.GetRawTransactionAsync(id));
    }

    public Task<RpcGetNetworkInfo> GetAnyNetworkInfoAsync()
    {
      return GetFirstSucesfullAsync(c => c.GetNetworkInfoAsync());
    }

    public Task<RpcGetTxOuts> GetTxOutsAsync(IEnumerable<(string txId, long N)> outpoints, string[] fieldList)
    {
      return GetFirstSucesfullAsync(c => c.GetTxOutsAsync(outpoints, fieldList));
    }
    
    public Task<RpcVerifyScriptResponse[]> VerifyScriptAsync(bool stopOnFirstInvalid,
                                        int totalTimeoutSec,
                                        IEnumerable<(string Tx, int N)> dsTx)
    {
      return GetFirstSucesfullAsync(c => c.VerifyScriptAsync(stopOnFirstInvalid, totalTimeoutSec, dsTx));
    }

    enum GroupType
    {
      OK,
      Known,
      Evicted,
      Invalid,
      MixedResult,
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

    Dictionary<string, ResponseTransactionType> CategorizeTransactions(
      RpcSendTransactions rpcResponse, string[] submittedTxids)
    {
      var processed =
        new Dictionary<string, ResponseTransactionType>(
          StringComparer.InvariantCulture);

      if (rpcResponse.Invalid != null)
      {
        foreach (var invalid in rpcResponse.Invalid)
        {
          processed.TryAdd(
            invalid.Txid, 
            new ResponseTransactionType
            {
              Type = GroupType.Invalid, 
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

    ResponseTransactionType ChooseNewValue(
      ResponseTransactionType oldValue,
      ResponseTransactionType newValue)
    {

      if (newValue.Type != oldValue.Type)
      {
        return new ResponseTransactionType { Type = GroupType.MixedResult, RejectCode = null, RejectReason = "Mixed results" };
      }

      return oldValue; // In case of different error messages we still treat the result as Error (not mixed)

    }


    /// <summary>
    /// Updates oldResults with data from newResults
    /// </summary>
    void AddNewResults(
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
      (byte[] transaction, bool allowhighfees, bool dontCheckFees, bool listUnconfirmedAncestors)[] transactions)
    {
      var allTxs = transactions.Select(x => Hashes.DoubleSHA256(x.transaction).ToString()).ToArray();

      var okResults = await GetAllWithoutErrors(c => c.SendRawTransactionsAsync(transactions), throwIfEmpty: true);


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

        // Treat mixed results as invalid transaction
        Invalid = results.Where(x => x.Value.Type == GroupType.Invalid || x.Value.Type == GroupType.MixedResult)
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
