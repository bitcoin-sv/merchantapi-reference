// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.Common.BitcoinRpc;
using MerchantAPI.APIGateway.Domain.Models;
using MerchantAPI.APIGateway.Domain.Models.Events;
using MerchantAPI.Common.EventBus;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MerchantAPI.Common.BitcoinRest;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin.Altcoins;
using MerchantAPI.APIGateway.Rest.ViewModels;
using System.Net.Http.Headers;
using MerchantAPI.APIGateway.Test.Functional.Server;
using MerchantAPI.APIGateway.Domain.ViewModels;
using System.Net.Mime;
using System.Net;

namespace MerchantAPI.APIGateway.Test.Functional
{
  /// <summary>
  /// Atribute to skip node start and registration
  /// </summary>
  public class SkipNodeStartAttribute : Attribute { }

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
    public IRestClient restClient0;
    public BitcoindProcess node0;

    public Queue<Coin> availableCoins = new Queue<Coin>();


    // Private key and corresponding address used for testing
    public const string testPrivateKeyWif = "cNpxQaWe36eHdfU3fo2jHVkWXVt5CakPDrZSYguoZiRHSz9rq8nF";
    public const string testAddress = "msRNSw5hHA1W1jXXadxMDMQCErX1X8whTk";

    EventBusSubscription<ZMQSubscribedEvent> zmqSubscribedEventSubscription;

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

      bool skipNodeStart = GetType().GetMethod(TestContext.TestName).GetCustomAttributes(true).Any(a => a.GetType() == typeof(SkipNodeStartAttribute));

      if (!skipNodeStart)
      {
        zmqSubscribedEventSubscription = eventBus.Subscribe<ZMQSubscribedEvent>();
        node0 = CreateAndStartNode(0);
        _ = zmqSubscribedEventSubscription.ReadAsync(CancellationToken.None).Result;
        rpcClient0 = node0.RpcClient;
        SetupChain(rpcClient0);
        restClient0 = node0.RestClient;
      }
    }

    public BitcoindProcess CreateAndStartNode(int index)
    {
      var bitcoind = StartBitcoind(index);

      var node = new Node(index, bitcoind.Host, bitcoind.RpcPort, bitcoind.RpcUser, bitcoind.RpcPassword, $"This is a mock node #{index}",
        null, (int)NodeStatus.Connected, null, null);

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
    public BitcoindProcess StartBitcoind(int nodeIndex, BitcoindProcess[] nodesToConnect = null)
    {

      string testPerfix = TestContext.FullyQualifiedTestClassName;
      if (testPerfix.StartsWith(commonTestPrefix))
      {
        testPerfix = testPerfix.Substring(commonTestPrefix.Length);
      }

      var dataDirRoot = Path.Combine(TestContext.TestRunDirectory, "node" + nodeIndex, testPerfix, TestContext.TestName);
      if (Environment.OSVersion.Platform == PlatformID.Win32NT && dataDirRoot.Length + bitcoindInternalPathLength >= 260)
      {
        // LevelDB refuses to open file with path length  longer than 260 
        throw new Exception($"Length of data directory path is too long. This might cause problems when running bitcoind on Windows. Please run tests from directory with a short path. Data directory path: {dataDirRoot}");
      }
      
      var bitcoind = new BitcoindProcess(
        bitcoindFullPath,
        dataDirRoot,
        nodeIndex, hostIp, zmqIp, loggerFactory,
        server.Services.GetRequiredService<IHttpClientFactory>(), nodesToConnect);
      bitcoindProcesses.Add(bitcoind);
      return bitcoind;
    }

    public void StopBitcoind(BitcoindProcess bitcoind)
    {
      if (!bitcoindProcesses.Contains(bitcoind))
      {
        throw new Exception($"Can not stop a bitcoind that was not started by {nameof(StartBitcoind)} ");
      }

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
    /// Sets ups a new chain, get some coins and store them in availableCoins, so that they can be consumed by test
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


    public async Task<uint256> GenerateBlockAndWaitForItTobeInsertedInDBAsync()
    {

      WaitUntilEventBusIsIdle(); // make sure that all old events (such activating ZMQ subscriptions) are processed
      var subscription = eventBus.Subscribe<NewBlockAvailableInDB>();
      try
      {

        loggerTest.LogInformation("Generating a block and waiting for it to be inserted in DB");
        // generate a new block
        var blockToWaitFor = new uint256((await rpcClient0.GenerateAsync(1))[0]);
        await WaitForEventBusEventAsync(subscription,
          $"Waiting for block {blockToWaitFor} to be inserted in DB",
          (evt) => new uint256(evt.BlockHash) == blockToWaitFor
        );
        return blockToWaitFor;
      }
      finally
      {
        eventBus.TryUnsubscribe(subscription);
      }
    }

    public async Task<(NBitcoin.Block, string)> MineNextBlockAsync(IEnumerable<NBitcoin.Transaction> transactions,
      bool throwOnError = true, string parentBlockHash = null)
    {
      if (string.IsNullOrEmpty(parentBlockHash))
      {
        parentBlockHash = await rpcClient0.GetBestBlockHashAsync();
      }

      var parentBlockBytes = await restClient0.GetBlockAsBytesAsync(parentBlockHash);
      var parentBlock = NBitcoin.Block.Load(parentBlockBytes, Network.RegTest);
      var parentBlockHeight = (await rpcClient0.GetBlockHeaderAsync(parentBlockHash)).Height;
      return await MineNextBlockAsync(transactions, throwOnError, parentBlock, parentBlockHeight);
    }

    public async Task<(NBitcoin.Block, string)> MineNextBlockAsync(IEnumerable<NBitcoin.Transaction> transactions, bool throwOnError, NBitcoin.Block parentBlock, long parentBlockHeight)
    {
      var newBlock = parentBlock.CreateNextBlockWithCoinbase(new Key().PubKey, parentBlockHeight, NBitcoin.Altcoins.BCash.Instance.Regtest.Consensus.ConsensusFactory);
      newBlock.Transactions.AddRange(transactions);
      newBlock.Header.Bits = parentBlock.Header.Bits; // assume same difficulty target
      newBlock.Header.BlockTime = parentBlock.Header.BlockTime.AddSeconds(1);
      newBlock.UpdateMerkleRoot();

      // Try to solve the block
      bool found = false;
      for (int i = 0; !found && i < 10000; i++)
      {
        newBlock.Header.Nonce = (uint)i;
        found = newBlock.Header.CheckProofOfWork();
      }

      if (!found)
      {
        throw new Exception("Bad luck - unable to find nonce that matches required difficulty");
      }

      var submitResult = await rpcClient0.SubmitBlock(newBlock.ToBytes());
      if (!string.IsNullOrEmpty(submitResult) && throwOnError)
      {
        throw new Exception($"Error while submitting new block - submitBlock returned {submitResult}");
      }

      return (newBlock, submitResult);
    }

    public (string txHex, string txId) CreateNewTransaction(Coin coin, Money amount)
    {
      var address = BitcoinAddress.Create(testAddress, Network.RegTest);
      var tx = BCash.Instance.Regtest.CreateTransaction();

      tx.Inputs.Add(new TxIn(coin.Outpoint));
      tx.Outputs.Add(coin.Amount - amount, address);

      var key = Key.Parse(testPrivateKeyWif, Network.RegTest);

      tx.Sign(key.GetBitcoinSecret(Network.RegTest), coin);

      return (tx.ToHex(), tx.GetHash().ToString());
    }

    public async Task<SubmitTransactionResponseViewModel> SubmitTransactionAsync(string txHex, bool merkleProof = false, bool dsCheck = false)
    {

      // Send transaction
      var reqContent = new StringContent(

        merkleProof || dsCheck ?
          $"{{ \"rawtx\": \"{txHex}\", \"merkleProof\": {merkleProof.ToString().ToLower()}, \"dsCheck\": {dsCheck.ToString().ToLower()}, \"callbackUrl\" : \"{Callback.Url}\"}}"
          :
          $"{{ \"rawtx\": \"{txHex}\" }}"
        );
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);

      var response =
        await Post<SignedPayloadViewModel>(MapiServer.ApiMapiSubmitTransaction, client, reqContent, HttpStatusCode.OK);

      return response.response.ExtractPayload<SubmitTransactionResponseViewModel>();
    }

    public async Task<SubmitTransactionsResponseViewModel> SubmitTransactionsAsync(string[] txHexList)
    {

      // Send transaction

      var reqJSON = "[{\"rawtx\": \"" + string.Join("\"}, {\"rawtx\": \"", txHexList) + "\"}]";
      var reqContent = new StringContent(reqJSON);
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);

      var response =
        await Post<SignedPayloadViewModel>(MapiServer.ApiMapiSubmitTransactions, client, reqContent, HttpStatusCode.OK);

      return response.response.ExtractPayload<SubmitTransactionsResponseViewModel>();
    }
  }
}
