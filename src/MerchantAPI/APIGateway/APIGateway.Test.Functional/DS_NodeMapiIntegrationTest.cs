// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Rest.ViewModels;
using MerchantAPI.APIGateway.Test.Functional.Server;
using MerchantAPI.Common.BitcoinRpc;
using MerchantAPI.Common.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Altcoins;
using NBitcoin.DataEncoders;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using MerchantAPI.APIGateway.Domain.ViewModels;

namespace MerchantAPI.APIGateway.Test.Functional
{
  [TestClass]
  public class DS_NodeMapiIntegrationTest : TestBaseWithBitcoind
  {
    IHost mapiHost;
    BitcoindProcess node1;

    [TestInitialize]
    public override void TestInitialize()
    {
      base.TestInitialize();

      InsertFeeQuote();
    }

    [TestCleanup]
    public override void TestCleanup()
    {
      StopBitcoind(node1);
      base.TestCleanup();
    }

    #region Setup live MAPI
    static void ConfigureWebHostBuilder(IWebHostBuilder webBuilder, string url)
    {
      var uri = new Uri(url);

      var hostAndPort = uri.Scheme + "://" + uri.Host + ":" + uri.Port;
      webBuilder.UseStartup<Rest.Startup>();
      webBuilder.UseUrls(hostAndPort);
      webBuilder.UseEnvironment("Testing");
      string appPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

      webBuilder.ConfigureAppConfiguration(cb =>
      {
        cb.AddJsonFile(Path.Combine(appPath, "appsettings.json"));
        cb.AddJsonFile(Path.Combine(appPath, "appsettings.development.json"), optional: true);
        cb.AddJsonFile(Path.Combine(appPath, "appsettings.test.functional.development.json"), optional: true);
      });

    }

    /// <summary>
    /// Starts a new instance of MAPI that is actually listening on port 5555, because TestServer is not actually listening on ports
    /// </summary>
    private void StartupLiveMAPI()
    {
      loggerTest.LogInformation("Starting up another instance of MAPI");
      mapiHost = Host.CreateDefaultBuilder(new string[0])
        .ConfigureWebHostDefaults(webBuilder => ConfigureWebHostBuilder(webBuilder, "http://localhost:5555")).Build();

      mapiHost.RunAsync();
    }

    private Task StopMAPI()
    {
      var cancellationToken = new CancellationTokenSource(1000).Token;
      return mapiHost.WaitForShutdownAsync(cancellationToken);
    }
    #endregion

    new (string txHex, string txId) CreateNewTransaction(Coin coin, Money amount)
    {
      var address = BitcoinAddress.Create(testAddress, Network.RegTest);
      var tx = BCash.Instance.Regtest.CreateTransaction();

      tx.Inputs.Add(new TxIn(coin.Outpoint));
      tx.Outputs.Add(coin.Amount - amount, address);

      var key = Key.Parse(testPrivateKeyWif, Network.RegTest);

      tx.Sign(key.GetBitcoinSecret(Network.RegTest), coin);

      return (tx.ToHex(), tx.GetHash().ToString());
    }

    async Task<SubmitTransactionsResponseViewModel> SubmitTransactions(string[] txHexList)
    {
      // Send transaction

      var reqJSON = "[{\"rawtx\": \"" + string.Join("\"}, {\"rawtx\": \"", txHexList) + "\", \"dscheck\": true, \"callbackurl\": \"http://mockCallback:8321\"}]";
      var reqContent = new StringContent(reqJSON);
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);

      var response =
        await Post<SignedPayloadViewModel>(MapiServer.ApiMapiSubmitTransactions, client, reqContent, HttpStatusCode.OK);

      return response.response.ExtractPayload<SubmitTransactionsResponseViewModel>();
    }

    /// <summary>
    /// Test requires 2 running nodes connected to each other...where 1st node also has mAPI connected and the 2nd node doesn't
    /// First transaction (that contains additional OP_RETURN output with DS protection protocol data) is submited through mAPI 
    /// to 1st node which then gets propagated to 2nd node.
    /// Second transaction (doublespend transaction) is submited directly to 2nd node, and mAPI should get notified that a
    /// double spend occured since 2nd node has found DS protection data submited by mAPI
    /// 
    /// There is also another mAPI that is started in this test, because mAPI running as TestServer doesn't actually listen on 
    /// any ports, so we start another mAPI to listen on port 5555
    /// </summary>
    /// <returns></returns>
    [Ignore("Test ignored untill CORE-955 and CORE-1223 will be merged into develop")]
    [TestMethod]
    public async Task SubmitTxsWithDSThatInitiatesCall2CallbackServerAsync()
    {
      // startup another node and link it to the first node
      node1 = StartBitcoind(1, new BitcoindProcess[] { node0 });

      StartupLiveMAPI();
      var coin = availableCoins.Dequeue();

      var address = BitcoinAddress.Create(testAddress, Network.RegTest);
      var tx1 = BCash.Instance.Regtest.CreateTransaction();

      #region Create a transaction with additional output for DoubleSpend protocol data
      tx1.Inputs.Add(new TxIn(coin.Outpoint));
      tx1.Outputs.Add(coin.Amount - new Money(1000L), address);

      var script = new Script(OpcodeType.OP_FALSE);
      script += OpcodeType.OP_RETURN;
      // Add protocol id
      script += Op.GetPushOp(Encoders.Hex.DecodeData("64736e74"));
      // The following hex data is in accordance with specs where:
      // 1st byte (0x01) is version number
      // 2nd byte (0x01) is number of IPv4 addresses (in this case only 1)
      // next 4 bytes (0x7f000001) is the IP address for 127.0.0.1
      // next byte (0x01) is the number of input ids that will be listed for checking (in this case only 1)
      // last byte (0x00) is the input id we want to be checked (in this case it's the n=0)
      script += Op.GetPushOp(Encoders.Hex.DecodeData("01017f0000010100"));
      var txOut = new TxOut(new Money(0L), script);
      tx1.Outputs.Add(txOut);

      var key = Key.Parse(testPrivateKeyWif, Network.RegTest);

      tx1.Sign(key.GetBitcoinSecret(Network.RegTest), coin);
      #endregion

      var tx1Hex = tx1.ToHex();
      var tx1Id = tx1.GetHash().ToString();

      var payload = await SubmitTransactions(new string[] { tx1Hex });

      loggerTest.LogInformation($"Submiting {tx1Id} with dsCheck enabled");
      var httpResponse = await PerformRequestAsync(client, HttpMethod.Get, MapiServer.ApiDSQuery + "/" + tx1Id);

      // Wait for tx to be propagated to node 1 before submiting a doublespend tx to node 1
      await Task.Delay(800);

      // Create double spend tx and submit it to node 1
      var (txHex2, txId2) = CreateNewTransaction(coin, new Money(1000L));

      loggerTest.LogInformation($"Submiting {txId2} with doublespend");
      await Assert.ThrowsExceptionAsync<RpcException>(async () => await node1.RpcClient.SendRawTransactionAsync(HelperTools.HexStringToByteArray(txHex2), true, false));

      await StopMAPI();
      var notifications = await TxRepositoryPostgres.GetTxsToSendMempoolDSNotificationsAsync();
      Assert.AreEqual(1, notifications.Count());
      Assert.AreEqual(txId2, new uint256(notifications.Single().DoubleSpendTxId).ToString());
     
    }
  }
}
