// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using MerchantAPI.Common.BitcoinRpc;
using MerchantAPI.Common.Json;
using NBitcoin;

namespace MerchantAPI.APIGateway.Test.Functional.Mock
{

  class BlockWithHeight
  {
    public long Height { get; set; }
    public uint256 BlockHash { get; set; }
    public byte[] BlockData { get; set; }

    public BlockHeader BlockHeader { get; set; }

  }

  public class RpcClientFactoryMock : IRpcClientFactory
  {
    public string mockedZMQNotificationsEndpoint = "tcp://127.0.0.1:28332";
    readonly ConcurrentDictionary<uint256, byte[]> transactions = new();
    readonly ConcurrentDictionary<uint256, BlockWithHeight> blocks = new();

    /// <summary>
    /// Key is nodeID:memberName value is value that should be returned to the caller
    /// </summary>
    public ConcurrentDictionary<string, object> PredefinedResponse  {get; private set; } = new ConcurrentDictionary<string, object>();

    /// <summary>
    /// Nodes that are not working
    /// </summary>
    readonly ConcurrentDictionary<string,object> disconnectedNodes = new(StringComparer.InvariantCultureIgnoreCase);
    readonly ConcurrentDictionary<string, object> doNotTraceMethods = new(StringComparer.InvariantCultureIgnoreCase);
    readonly IList<(string, int)> validScriptCombinations = new List<(string, int)>();
    

    public RpcClientFactoryMock()
    {
      Reset();
    }

    /// <summary>
    /// Replaces currently known transactions with a set of new transactions
    /// </summary>
    /// <param name="data"></param>
    public void SetUpTransaction(params string[] data)
    {
      transactions.Clear();
      foreach (var tx in data)
      {
        AddKnownTransaction(HelperTools.HexStringToByteArray(tx));
      }
    }

    public void SetUpPredefinedResponse(params (string callKey, object obj)[] responses)
    {
      PredefinedResponse = new ConcurrentDictionary<string, object>(
        responses.ToDictionary(x => x.callKey, v => v.obj));

    }
    public void AddKnownTransaction(byte[] data) 
    {
      var txId = Transaction.Load(data, Network.Main).GetHash(); // might not handle very large transactions
      transactions.TryAdd(txId, data);
    }

    public void AddKnownBlock(long blockHeight, byte[] blockData)
    {
      var block = Block.Load(blockData, Network.Main);
      var blockHash = block.GetHash();
      var b = new BlockWithHeight
      {
        Height = blockHeight,
        BlockData = blockData,
        BlockHash = blockHash,
        BlockHeader = block.Header
      };
      blocks.TryAdd(blockHash, b);
    }

    public void AddScriptCombination(string tx, int n)
    {
      validScriptCombinations.Add((tx, n));
    }

    public readonly RpcCallList AllCalls = new(); 

    public virtual IRpcClient Create(string host, int port, string username, string password) 
    {
      // Currently all mocks share same transactions and blocks
      return new RpcClientMock(AllCalls, host, port, username, password, mockedZMQNotificationsEndpoint,
        transactions,
        blocks, disconnectedNodes, doNotTraceMethods, PredefinedResponse,
        validScriptCombinations);
    }

    /// <summary>
    /// Asserts that call lists equals to expected value and clears it so that new calls can be easily
    /// testes in next invocation of AssertEqualAndClear
    /// </summary>
    /// <param name="expected"></param>
    public void AssertEqualAndClear(params string[] expected)
    {
      AllCalls.AssertEqualTo(expected);
      ClearCalls();
    }


    /// <summary>
    /// Clear all calls to bitcoind
    /// </summary>
    public void ClearCalls()
    {
      AllCalls.ClearCalls();
    }

    /// <summary>
    /// Reset nodes. We currently keep tx  and block data
    /// </summary>
    public void Reset()
    {
      ClearCalls();
      ReconnecNodes();
      doNotTraceMethods.Clear();
    }

    public void DisconnectNode(string nodeId)
    {
      disconnectedNodes.TryAdd(nodeId,null);
    }

    public void ReconnectNode(string nodeId)
    {
      disconnectedNodes.TryRemove(nodeId, out _);
    }

    public void ReconnecNodes()
    {
      disconnectedNodes.Clear();
    }

  }
}
