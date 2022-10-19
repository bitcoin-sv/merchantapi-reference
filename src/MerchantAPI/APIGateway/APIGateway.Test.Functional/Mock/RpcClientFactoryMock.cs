// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
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
    public string StreamFilename { get; set; }

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
    
    readonly ConcurrentDictionary<string, HashSet<string>> ignoredTransactions = new();

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
      var oldblockHash = blocks.SingleOrDefault(x => x.Value.Height == blockHeight).Key;
      if (oldblockHash != null)
      {
        blocks.Remove(oldblockHash, out var value);
      }

      blocks.TryAdd(blockHash, b);
    }

    public void AddBigKnownBlock(long blockHeight, Block block)
    {
      string filename = null;
      byte[] blockData = GetBytesFromBlock(block);
      if (blockData == null) // too big block for Memorystream
      {
        filename = SaveStreamFromBlock(block);
      }

      var blockHash = block.GetHash();
      var b = new BlockWithHeight
      {
        Height = blockHeight,
        BlockHash = blockHash,
        BlockHeader = block.Header,
        BlockData = blockData,
        StreamFilename = filename
      };
      blocks.TryAdd(blockHash, b);
    }

    public bool RemoveKnownBlock(uint256 blockHash)
    {
      return blocks.TryRemove(blockHash, out var _);
    }

    /// <summary>
    /// This method has block size limitation because of the Memorystream. The maximum index in any single dimension 
    /// is 2,147,483,591(0x7FFFFFC7) for byte arrays and arrays of single byte structures, 
    /// and 2,146,435,071(0X7FEFFFFF) for other types.
    /// </summary>
    /// <param name="b">NBitcoin block.</param>
    /// <returns>Block bytes.</returns>
    public static byte[] GetBytesFromBlock(Block b)
    {
      byte[] objectBytes = null;
      using var ms = new MemoryStream();
      BitcoinStream s = new(ms, true)
      {
        MaxArraySize = unchecked((int)uint.MaxValue)
      };

      try
      {
        b.ReadWrite(s);
        objectBytes = ms.ToArray();
      }
      catch (IOException)
      {
        // block size bigger than 2.1GB
      }
      return objectBytes;
    }

    public static string SaveStreamFromBlock(Block b)
    {
      // create FileStream, so that we support bigger blocks from 2.2GB
      var fileName = @"Data/big_block.txt";

      using FileStream fs = File.Create(fileName);

      BitcoinStream s = new(fs, true)
      {
        MaxArraySize = unchecked((int)uint.MaxValue) // NBitcoin internally casts to uint when comparing
      };

      b.ReadWrite(s);

      return fileName;
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
        validScriptCombinations,
        ignoredTransactions.ContainsKey(host) ? ignoredTransactions[host]: new HashSet<string>());
    }

    /// <summary>
    /// RpcClient settings are ignored.
    /// </summary>
    public virtual IRpcClient Create(string host, int port, string username, string password,
        int requestTimeoutSec, int multiRequestTimeoutSec, int numOfRetries, int waitBetweenRetriesMs) 
    {
      return Create(host, port, username, password);
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

    public void IgnoreTransactionOnNode(string nodeId, string txId)
    {
      if (!ignoredTransactions.ContainsKey(nodeId))
      {
        ignoredTransactions[nodeId] = new HashSet<string>();
      }
      ignoredTransactions[nodeId].Add(txId);
    }

    public void ReconnectNode(string nodeId)
    {
      disconnectedNodes.TryRemove(nodeId, out _);
    }

    public void ReconnecNodes()
    {
      disconnectedNodes.Clear();
    }

    public void CleanupBigBlocks()
    {
      foreach (var b in blocks.Where(x => !string.IsNullOrEmpty(x.Value.StreamFilename)))
      {
        File.Delete(b.Value.StreamFilename);
      }
      GC.Collect();
    }
  }
}
