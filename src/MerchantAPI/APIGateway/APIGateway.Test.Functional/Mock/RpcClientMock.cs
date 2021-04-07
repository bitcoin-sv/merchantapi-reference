// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using MerchantAPI.APIGateway.Domain;
using MerchantAPI.Common.BitcoinRest;
using MerchantAPI.Common.BitcoinRpc;
using MerchantAPI.Common.BitcoinRpc.Responses;
using MerchantAPI.Common.Json;
using NBitcoin;

namespace MerchantAPI.APIGateway.Test.Functional.Mock
{

  class RpcClientMock : IRpcClient, IRestClient
  {
    RpcCallList callList;
    string nodeId;
    ConcurrentDictionary<uint256, byte[]> transactions;
    ConcurrentDictionary<uint256, BlockWithHeight> blocks;
    ConcurrentDictionary<string, object> disconnectedNodes;
    ConcurrentDictionary<string, object> doNotTraceMethods;
    IList<(string, int)> validScriptCombinations;

    // Key is nodeID:memberName value is value that should be returned to the caller
    private ConcurrentDictionary<string, object> predefinedResponse;

    public TimeSpan RequestTimeout { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    public int NumOfRetries { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public RpcClientMock(RpcCallList callList, string host, int port, string username, string password, 
      ConcurrentDictionary<uint256, byte[]> transactions,
      ConcurrentDictionary<uint256, BlockWithHeight> blocks,
      ConcurrentDictionary<string, object> disconnectedNodes,
      ConcurrentDictionary<string, object> doNotTraceMethods,
      ConcurrentDictionary<string, object> predefinedResponse,
      IList<(string, int)> validScriptCombinations
      )
    {
      this.callList = callList;
      nodeId = host;
      this.transactions = transactions;
      this.blocks = blocks;
      this.disconnectedNodes = disconnectedNodes;
      this.doNotTraceMethods = doNotTraceMethods;
      this.predefinedResponse = predefinedResponse;
      this.validScriptCombinations = validScriptCombinations;
    }

    public void ThrowIfDisconnected()
    {
      if (disconnectedNodes.ContainsKey(nodeId))
      {
        throw new HttpRequestException($"Node '{nodeId}' can not be reached (simulating error)");
      }
    }
        
    /// <summary>
    /// Throws if node is disconnected. Records successful call in call lists.
    /// Return non null if predefined result should be returned to called
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="txids"></param>
    /// <param name="memberName"></param>
    /// <returns></returns>
    T SimulateCall<T>(string txids = null, [CallerMemberName] string memberName = "")
    {
      ThrowIfDisconnected();

      // Strip off async suffix
      const string asyncSuffix = "async";
      memberName = memberName.ToLowerInvariant();
      if (memberName.EndsWith(asyncSuffix))
      {
        memberName = memberName.Substring(0, memberName.Length - asyncSuffix.Length);
      }

      if (predefinedResponse.TryGetValue(nodeId + ":" + memberName, out var responseObj))
      {
        return (T) responseObj;
      }

      if (doNotTraceMethods!=null && doNotTraceMethods.ContainsKey(memberName))
      {
        return default(T);
      }

      callList?.AddCall(memberName, nodeId, txids);

      return default(T);
    }

    public Task<long> GetBlockCountAsync(CancellationToken? token = null)
    {
      var r = SimulateCall<long?>();
      if (r.HasValue)
      {
        return Task.FromResult(r.Value);
      }

      return Task.FromResult(blocks.Values.OrderByDescending(x => x.Height).First().Height);
    }

    public Task<RpcGetBlockWithTxIds> GetBlockWithTxIdsAsync(string blockHash, CancellationToken? token = null)
    {
      throw new NotImplementedException();
    }

    public Task<RpcGetBlock> GetBlockAsync(string blockHash, int verbosity, CancellationToken? token = null)
    {
      throw new NotImplementedException();
    }

    public Task<byte[]> GetBlockAsBytesAsync(string blockHash, CancellationToken? token = null)
    {
      var r = SimulateCall<byte[]>();
      if (r != null)
      {
        return Task.FromResult(r);
      }
      
      if (!blocks.TryGetValue(new uint256(blockHash), out var block))
      {
        throw new Exception($"Mock block {blockHash} not found");
      }

      return Task.FromResult(block.BlockData);
    }
    
    public Task<byte[]> GetBlockByHeightAsBytesAsync(long blockHeight, CancellationToken? token = null)
    {
      var r = SimulateCall<byte[]>();
      if (r != null)
      {
        return Task.FromResult(r);
      }

      if ((blocks.Count - 1) < blockHeight)
      {
        throw new Exception($"Mock block with height {blockHeight} not found");
      }
      var block = blocks.ElementAt((int)blockHeight).Value;

      return Task.FromResult(block.BlockData);
    }

    public Task<string> GetBlockHashAsync(long height, CancellationToken? token = null)
    {
      var r = SimulateCall<string>();
      if (r != null)
      {
        return Task.FromResult(r);
      }

      return Task.FromResult(blocks.Values.Single(x => x.Height == height).BlockHash.ToString());
    }

    public Task<RpcGetBlockHeader> GetBlockHeaderAsync(string blockHash, CancellationToken? token = null)
    {
      var r = SimulateCall<RpcGetBlockHeader>();
      if (r != null)
      {
        return Task.FromResult(r);
      }

      if (!blocks.TryGetValue(new uint256(blockHash), out var block))
      {
        throw new Exception($"Mock block {blockHash} not found");
      }

      var header = block.BlockHeader;
      var result = new RpcGetBlockHeader
      {
        Hash = blockHash,
        Confirmations = 666, // Mock
        Height = block.Height,
        Version = header.Version,
        VersionHex = header.Version.ToString("X8"),
        Merkleroot = header.HashMerkleRoot.ToString(),
        NumTx = 99999, // MOCK value,
        Time = header.BlockTime.ToUnixTimeSeconds(),
        Mediantime = header.BlockTime.ToUnixTimeSeconds(), // we can not return the right value here, sine we are not racking chain
        Nonce = header.Nonce,
        //Bits = header.Bits.ToString(),
        Difficulty = 0, // MOCK value
        Chainwork = "0", // MOCK value
        Previousblockhash = header.HashPrevBlock.ToString()
      };
      return Task.FromResult(result);
    }

    public Task<string> GetBlockHeaderAsHexAsync(string blockHash, CancellationToken? token = null)
    {
      throw new NotImplementedException();
    }

    public Task<RpcGetRawTransaction> GetRawTransactionAsync(string txId, int retryCount, CancellationToken? token = null)
    {
      var r = SimulateCall<RpcGetRawTransaction>();
      if (r != null)
      {
        return Task.FromResult(r);
      }

      if (transactions.TryGetValue(new uint256(txId), out _))
      {
        return Task.FromResult(
          new RpcGetRawTransaction
          {
            Txid = txId,
            // other fields are not mapped
          }
          );
      }

      throw new Exception($"TxId {txId} not found");
    }

    public Task<byte[]> GetRawTransactionAsBytesAsync(string txId, CancellationToken? token = null)
    {
      var r = SimulateCall<byte[]>();
      if (r != null)
      {
        return Task.FromResult(r);
      }

      if (transactions.TryGetValue(new uint256(txId), out var result))
      {
        return Task.FromResult(result);
      }
    
      throw new Exception($"TxId {txId} not found");
    }

    public Task<string> GetBestBlockHashAsync(CancellationToken? token = null)
    {
      var r = SimulateCall<string>();
      if (r != null)
      {
        return Task.FromResult(r);
      }

      if (blocks.IsEmpty)
      {
        throw new Exception($"No bock has been added to RpcClientMock");
      }
      return Task.FromResult(blocks.Values.OrderByDescending(x => x.Height).First().BlockHash.ToString());
    }

    public Task<string> SendRawTransactionAsync(byte[] transaction, bool allowhighfees, bool dontCheckFees, CancellationToken? token = null)
    {
      var txId = NBitcoin.Transaction.Parse(HelperTools.ByteToHexString(transaction), Network.Main).GetHash()
        .ToString();

      var r = SimulateCall<string>(txId);
      if (r != null)
      {
        return Task.FromResult(r);
      }

      return Task.FromResult(txId);
    }

    public Task<RpcSendTransactions> SendRawTransactionsAsync((byte[] transaction, bool allowhighfees, bool dontCheckFees, bool listUnconfirmedAncestors)[] txs,
      CancellationToken? token = null)
    {
      var txIds = 
        string.Join('/',txs.Select(x =>
        NBitcoin.Transaction.Parse(HelperTools.ByteToHexString(x.transaction), Network.Main).GetHash().ToString()).ToArray());

      var r = SimulateCall<RpcSendTransactions>(txIds);
      if (r != null)
      {
        return Task.FromResult(r);
      }

      return Task.FromResult
        (
          new RpcSendTransactions() // empty response means that everything was accepted
        );
    }
    public Task<RpcGetNetworkInfo> GetNetworkInfoAsync(CancellationToken? token=null)
    {
      var r = SimulateCall<RpcGetNetworkInfo>();
      if (r != null)
      {
        return Task.FromResult(r);
      }

      return Task.FromResult(
        new RpcGetNetworkInfo
        {
          MinConsolidationFactor = 20,
          MaxConsolidationInputScriptSize = 150,
          MinConsolidationInputMaturity = 6,
          AcceptNonStdConsolidationInput = false
        });
    }

    public Task<RpcGetTxOuts> GetTxOutsAsync(IEnumerable<(string txId, long N)> outpoints, string[] fieldList, CancellationToken? token = null)
    {
      var r = SimulateCall<RpcGetTxOuts>();
      if (r != null)
      {
        return Task.FromResult(r);
      }

      var results = new List<PrevOut>();
      foreach (var (txId, N) in outpoints)
      {

        PrevOut result = null;
        if (transactions.TryGetValue(new uint256(txId), out var foundTx))
        {
          var outputs = HelperTools.ParseBytesToTransaction(foundTx).Outputs;
          if (N < outputs.Count)
          {
            var output = outputs[(int) N];
            result = new PrevOut
            {
              Error = null,
              ScriptPubKeyLength = output.ScriptPubKey.Length,
              ScriptPubKey = output.ScriptPubKey.ToHex(),
              Value = output.Value.ToDecimal(MoneyUnit.BTC),
              // Mock values - they are not correct:
              Confirmations = 0,
              IsStandard = true
            };
          }
        }

        result ??= new PrevOut
        {
          Error = "missing"
        };

        results.Add(result);
      }

      return
        Task.FromResult(
        new RpcGetTxOuts
        {
          TxOuts = results.ToArray()
        }
        );
    }

    public Task<string> SubmitBlock(byte[] block, CancellationToken? token = null)
    {
      var r = SimulateCall<string>();
      if (r != null)
      {
        return Task.FromResult(r);
      }

      return Task.FromResult<string>(null);
    }

    public Task StopAsync(CancellationToken? token = null)
    {
      throw new NotImplementedException(); // We could add the node to list of disconnected nodes
    }

    public Task<string[]> GenerateAsync(int n, CancellationToken? token = null)
    {
      throw new NotImplementedException();
    }

    public Task<string> SendToAddressAsync(string address, double amount, CancellationToken? token = null)
    {
      throw new NotImplementedException();
    }

    public Task<RpcGetMerkleProof> GetMerkleProofAsync(string txId, string blockHash, CancellationToken? token = null)
    {
      var r = SimulateCall<RpcGetMerkleProof>();
      if (r != null)
      {
        return Task.FromResult(r);
      }

      return Task.FromResult(new RpcGetMerkleProof());
    }

    public Task<RpcGetBlockchainInfo> GetBlockchainInfoAsync(CancellationToken? token = null)
    {
      var r = SimulateCall<RpcGetBlockchainInfo>();
      if (r != null)
      {
        return Task.FromResult(r);
      }

      if (blocks.IsEmpty)
      {
        throw new Exception($"No bock has been added to RpcClientMock");
      }

      var bestBlock = blocks.Values.OrderByDescending(x => x.Height).First();
      return Task.FromResult(
        new RpcGetBlockchainInfo
        {
          Chain = null,
          Blocks = bestBlock.Height,
          Headers = bestBlock.Height,
          BestBlockHash = bestBlock.BlockHash.ToString()
        });
    }

    public Task<RpcActiveZmqNotification[]> ActiveZmqNotificationsAsync(CancellationToken? token = null)
    {
      var r = SimulateCall<RpcActiveZmqNotification[]>();
      if (r != null)
      {
        return Task.FromResult(r);
      }

      return Task.FromResult(ZMQTopic.RequiredZmqTopics.Select(x => new RpcActiveZmqNotification { Address = "tcp://127.0.0.1:28332", Notification = x}).ToArray());
    }
    public Task<string[]> GetRawMempool(CancellationToken? token = null)
    {
      var r = SimulateCall<string[]>();
      if (r != null)
      {
        return Task.FromResult(r);
      }
      return Task.FromResult(new string[0]);
    }

    public Task<RpcVerifyScriptResponse[]> VerifyScriptAsync(bool stopOnFirstInvalid, 
                                                                 int totalTimeoutSec,
                                                                 IEnumerable<(string Tx, int N)> dsTx, CancellationToken? token)
    {
      var results = new List<RpcVerifyScriptResponse>();
      foreach (var tx in dsTx)
      {
        if (validScriptCombinations.Contains(tx))
        {
          results.Add(new RpcVerifyScriptResponse { Result = "ok" });
        }
        else
        {
          results.Add(new RpcVerifyScriptResponse { Result = "error" });
        }
      }

      return Task.FromResult(results.ToArray());
    }

  }

}
