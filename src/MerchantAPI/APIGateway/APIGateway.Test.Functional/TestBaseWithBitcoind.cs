// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MerchantAPI.APIGateway.Domain.Models;
using MerchantAPI.Common.BitcoinRpc;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NBitcoin;

namespace MerchantAPI.APIGateway.Test.Functional
{
  /// <summary>
  /// base class for functional tests that require bitcoind instance
  /// During test setup a new instance of bitcoind is setup, some blocks are generated and coins are collected,
  /// so that they can be used during test
  /// </summary>
  public class TestBaseWithBitcoind : TestBase
  {
    private string bitcoindFullPath;
    private string hostIp = "localhost";
    private string zmqIp = "127.0.0.1";
    public TestContext TestContext { get; set; }

    protected List<BitcoindProcess> bitcoindProcesses = new List<BitcoindProcess>();

    public IRpcClient rpcClient0; 
    public BitcoindProcess node0;

    public Queue<Coin> availableCoins = new Queue<Coin>();


    // Private key and corresponding address used for testing
    public const string testPrivateKeyWif = "cNpxQaWe36eHdfU3fo2jHVkWXVt5CakPDrZSYguoZiRHSz9rq8nF";
    public const string testAddress = "msRNSw5hHA1W1jXXadxMDMQCErX1X8whTk";


    public virtual void TestInitialize()
    {
      Initialize(mockedServices: false);

      var bitcoindConfigKey = "BitcoindFullPath";
      bitcoindFullPath = Configuration[bitcoindConfigKey];
      if (string.IsNullOrEmpty(bitcoindFullPath))
      {
        throw new Exception($"Required parameter {bitcoindConfigKey} is missing from configuration");
      }
      
      var alternativeIp = Configuration["HostIp"];
      if (!string.IsNullOrEmpty(alternativeIp))
      {
        hostIp = alternativeIp;
      }

      node0 = CreateAndStartNode(0);
      rpcClient0 = node0.RpcClient;
      SetupChain(rpcClient0);
    }

    public BitcoindProcess CreateAndStartNode(int index)
    {
      var bitcoind = StartBitcoind(index);

      var node = new Node(index, bitcoind.Host, bitcoind.RpcPort, bitcoind.RpcUser, bitcoind.RpcPassword, $"This is a mock node #{index}",
        (int)NodeStatus.Connected, null, null);

      _ = Nodes.CreateNodeAsync(node).Result;
      return bitcoind;
    }

    void StopAllBitcoindProcesses()
    {
      if (bitcoindProcesses.Any())
      {
        var totalCount = bitcoindProcesses.Count;
        int sucesfullyStopped = 0;
        loggerTest.LogInformation($"Shutting down {totalCount} bitcoind processes");

        foreach (var bitcoind in bitcoindProcesses.ToArray())
        {
          var bitcoindDescription = bitcoind.Host + ":" + bitcoind.RpcPort;
          try
          {
            StopBitcoind(bitcoind);
            sucesfullyStopped++;
          }
          catch (Exception e)
          {
            loggerTest.LogInformation($"Error while stopping bitcoind {bitcoindDescription}. This can occur if node has been explicitly stopped or if it crashed. Will proceed anyway. {e}");
          }

          loggerTest.LogInformation($"Successfully stopped {sucesfullyStopped} out of {totalCount} bitcoind processes");

        }
        bitcoindProcesses.Clear();
      }
    }
    public virtual void TestCleanup()
    {
      // StopAllBitcoindProcesses is called after the server is shutdown, so that we are sure that no
      // no background services (which could use bitcoind)  are still running
      Cleanup(StopAllBitcoindProcesses);
    }

    static readonly string commonTestPrefix = typeof(TestBaseWithBitcoind).Namespace + ".";
    static readonly int bitcoindInternalPathLength = "regtest/blocks/index/MANIFEST-00000".Length + 10;
    public BitcoindProcess StartBitcoind(int nodeIndex)
    {
     
      string testPerfix = TestContext.FullyQualifiedTestClassName;
      if (testPerfix.StartsWith(commonTestPrefix))
      {
        testPerfix = testPerfix.Substring(commonTestPrefix.Length);
      }

      var dataDirRoot = Path.Combine(TestContext.TestRunDirectory, "node" + nodeIndex,  testPerfix, TestContext.TestName);
      if (Environment.OSVersion.Platform == PlatformID.Win32NT && dataDirRoot.Length + bitcoindInternalPathLength >= 260)
      {
        // LevelDB refuses to open file with path length  longer than 260 
        throw new Exception($"Length of data directory path is too long. This might cause problems when running bitcoind on Windows. Please run tests from directory with a short path. Data directory path: {dataDirRoot}");
      } 
      var bitcoind = new BitcoindProcess(
        bitcoindFullPath,
        dataDirRoot,
        nodeIndex, hostIp, zmqIp, loggerFactory);
      bitcoindProcesses.Add(bitcoind);
      return bitcoind;
    }

    public void StopBitcoind(BitcoindProcess bitcoind)
    {
      if (!bitcoindProcesses.Contains(bitcoind))
      {
        throw new Exception($"Can not stop a bitcoind that was not started by {nameof(StartBitcoind)} ");
      }

      bitcoind.RpcClient.StopAsync().Wait(); // stop is implemented as aync in bitcoind, but it should stop receiving new RPC requests before it returns
      bitcoind.Dispose();

    }

    static Coin GetCoin(IRpcClient rpcClient)
    {
      var txId = rpcClient.SendToAddressAsync(testAddress, 0.1).Result;
      var tx = NBitcoin.Transaction.Load(rpcClient.GetRawTransactionAsBytesAsync(txId).Result, Network.RegTest);
      int? foundIndex = null;
      for (int i = 0; i < tx.Outputs.Count; i++)
      {
        if (tx.Outputs[i].ScriptPubKey.GetDestinationAddress(Network.RegTest).ToString() == testAddress)
        {
          foundIndex = i;
          break;
        }
      }

      if (foundIndex == null)
      {
        throw new Exception("Unable to found a transaction output with required destination address");
      }

      return new Coin(tx, (uint)foundIndex.Value);
    }

    protected static Coin[] GetCoins(IRpcClient rpcClient, int number)
    {
      var coins = new List<Coin>();
      for (int i = 0; i < number; i++)
      {
        coins.Add(GetCoin(rpcClient));
      }

      // Mine coins into  a block
      _ = rpcClient.GenerateAsync(1).Result;

      return coins.ToArray();

    }


    /// <summary>
    /// Sets ups a new chain, get some coins and store them in availableCOins, so that they can be consumed by test
    /// </summary>
    /// <param name="rpcClient"></param>
    public void SetupChain(IRpcClient rpcClient)
    {
      loggerTest.LogInformation("Setting up test chain");
      _ = rpcClient.GenerateAsync(150).Result;
      foreach (var coin in GetCoins(rpcClient, 10))
      {
        availableCoins.Enqueue(coin);
      }
    }
  }
}
