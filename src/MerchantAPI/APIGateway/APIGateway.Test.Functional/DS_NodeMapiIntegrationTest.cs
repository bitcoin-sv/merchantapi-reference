// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Test.Functional.Server;
using MerchantAPI.Common.BitcoinRpc;
using MerchantAPI.Common.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NBitcoin;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using System.Collections.Generic;
using MerchantAPI.APIGateway.Domain.Models;
using NBitcoin.DataEncoders;
using Serilog;
using Serilog.Events;

namespace MerchantAPI.APIGateway.Test.Functional
{
  [TestCategory("TestCategoryNo4")]
  [TestClass]
  public class DS_NodeMapiIntegrationTest : DS_NodeMapiTestBase
  {
    IHost mapiHost;
    BitcoindProcess node1;

    [TestInitialize]
    public override void TestInitialize()
    {
      base.TestInitialize();
    }

    [TestCleanup]
    public override void TestCleanup()
    {
      if (mapiHost != null)
      {
        StopMAPI().Wait();
      }
      base.TestCleanup();
      if (mapiHost != null)
      {
        // mAPI must be stopped in TestCleanup because it can happen,
        // that assert fails or an error is thrown
        StopMAPI().Wait();
      }
    }

    #region Setup live MAPI
    static void ConfigureWebHostBuilder(IWebHostBuilder webBuilder, string url, string dataDirRoot)
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

     var logger = new LoggerConfiguration()
      .MinimumLevel.Debug()
      .MinimumLevel.Override("Microsoft", LogEventLevel.Debug)
      .MinimumLevel.Override("System", LogEventLevel.Debug)
      .ReadFrom.AppSettings()
      .WriteTo.File($"{dataDirRoot}/mapi.txt", shared: true,
                    outputTemplate: "{Timestamp:u} [{Level:u3}] {Message:lj}{NewLine}{Exception}")

      .CreateLogger();
    webBuilder.ConfigureLogging((hostingContext, builder) =>
    {
      builder.AddSerilog(logger);
    });

    }

    /// <summary>
    /// Starts a new instance of MAPI that is actually listening on port 5555, because TestServer is not actually listening on ports
    /// </summary>
    private void StartupLiveMAPI(int DSPort=5555)
    {
      loggerTest.LogInformation("Starting up another instance of MAPI");
      var dataDirRoot = Path.Combine(TestContext.TestRunDirectory, "mapi", TestContext.TestName);

      mapiHost = Host.CreateDefaultBuilder(Array.Empty<string>())
        .ConfigureWebHostDefaults(webBuilder => ConfigureWebHostBuilder(
          webBuilder,
          $"http://localhost:{DSPort}",
          dataDirRoot
        )).Build();

      mapiHost.RunAsync();
    }
    private void StartupNode1AndLiveMAPI()
    {
      // startup another node and link it to the first node
      node1 = StartBitcoind(1, new BitcoindProcess[] { node0 }, argumentList: new() { "-debug=doublespend" });

      StartupLiveMAPI();
    }

    private async Task StopMAPI()
    {
      var cancellationToken = new CancellationTokenSource(1000).Token;
      await mapiHost.WaitForShutdownAsync(cancellationToken);
    }
    #endregion

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
    [DataRow(true)]
    [DataRow(false)]
    [TestMethod]
    public async Task SubmitTxsWithDSCallback(bool IPv4)
    {
      StartupNode1AndLiveMAPI();

      var coin = availableCoins.Dequeue();

      var tx1 = CreateDS_OP_RETURN_Tx(new Coin[] { coin }, IPv4: IPv4, DSprotectedInputs: 00);

      await CheckDsNotifications(tx1, coin, 1, sendToNode1: true);

      //Create another DS tx which should not trigger another notification
      var (txHex3, txId3) = CreateNewTransaction(coin, new Money(5000L));

      loggerTest.LogInformation($"Submiting {txId3} with doublespend");
      await Assert.ThrowsExceptionAsync<RpcException>(async () => await node1.RpcClient.SendRawTransactionAsync(HelperTools.HexStringToByteArray(txHex3), true, false));

      // Wait for a bit for node and Live mAPI to process all events
      await Task.Delay(3000);

      var notifications = await TxRepositoryPostgres.GetNotificationsForTestsAsync();
      Assert.AreEqual(1, notifications.Length);
    }

    private async Task CheckDsNotifications(Transaction tx1, Coin coin, int expectedDSNotifications, bool sendToNode1 = false)
    {
      var tx1Hex = tx1.ToHex();
      var tx1Id = tx1.GetHash().ToString();

      loggerTest.LogInformation($"Submiting {tx1Id} with dsCheck enabled");
      var payload = await SubmitTransactions(new string[] { tx1Hex });

      if (sendToNode1)
      {
        var httpResponse = await PerformRequestAsync(Client, HttpMethod.Get, MapiServer.ApiDSQuery + "/" + tx1Id);

        // Wait for tx to be propagated to node 1 before submiting a doublespend tx to node 1
        using CancellationTokenSource cts = new(30000);
        await WaitForTxToBeAcceptedToMempool(node1, tx1Id, cts.Token);
      }

      // Create double spend tx and submit it to node
      var (txHex2, txId2) = CreateNewTransaction(coin, new Money(1000L));

      loggerTest.LogInformation($"Submiting {txId2} with doublespend");
      BitcoindProcess nodeToSend = sendToNode1 ? node1 : node0;
      await Assert.ThrowsExceptionAsync<RpcException>(async () => await nodeToSend.RpcClient.SendRawTransactionAsync(HelperTools.HexStringToByteArray(txHex2), true, false));

      // Wait for a bit for node and Live mAPI to process all events
      await Task.Delay(3000);

      loggerTest.LogInformation("Retrieving notification data");
      var notifications = await TxRepositoryPostgres.GetNotificationsForTestsAsync();
      Assert.AreEqual(expectedDSNotifications, notifications.Length);
      if (expectedDSNotifications > 0)
      {
        Assert.AreEqual(txId2, new uint256(notifications.Single().DoubleSpendTxId).ToString());
      }
    }

    [TestMethod]
    public async Task SubmitTxWithDsCheckAndOP_RETURN()
    {
      StartupLiveMAPI();

      var coin = availableCoins.Dequeue();

      var tx1 = CreateDS_OP_RETURN_Tx(new Coin[] { coin }, DSprotectedInputs: 00);

      await CheckDsNotifications(tx1, coin, 1, sendToNode1: false);
    }

    [TestMethod]
    public async Task MultipleDSQueriesReturn1Notification()
    {
      using CancellationTokenSource cts = new(30000);

      StartupLiveMAPI();

      int noOfNodes = 4;
      List<BitcoindProcess> nodeList = new()
      {
        node0
      };
      for (int i=1; i <= noOfNodes; i++)
      {
        nodeList.Add(StartBitcoind(i, nodeList.ToArray()));
      }

      await SyncNodesBlocksAsync(cts.Token, nodeList.ToArray());

      var coin = availableCoins.Dequeue();

      var tx1 = CreateDS_OP_RETURN_Tx(new Coin[] { coin }, DSprotectedInputs: 00);
      var tx1Hex = tx1.ToHex();
      var tx1Id = tx1.GetHash().ToString();

      loggerTest.LogInformation($"Submiting {tx1Id} with doublespend notification enabled");
      var payload = await SubmitTransactions(new string[] { tx1Hex });

      // Wait for tx to be propagated to all nodes before submiting a doublespend tx to nodes
      List<Task> mempoolTasks = new();
      for (int i = 1; i <= noOfNodes; i++)
      {
        mempoolTasks.Add(WaitForTxToBeAcceptedToMempool(nodeList[i], tx1Id, cts.Token));
      }
      await Task.WhenAll(mempoolTasks);


      // Create double spend tx and submit it to all nodes except the one connected to mAPI
      var (txHex2, txId2) = CreateNewTransaction(coin, new Money(1000L));

      loggerTest.LogInformation($"Submiting {txId2} with doublespend to all running nodes at once");
      List<Task<RpcException>> taskList = new();
      for (int i = 1; i <= noOfNodes; i++)
      {
        taskList.Add(Assert.ThrowsExceptionAsync<RpcException>(async () => await nodeList[i].RpcClient.SendRawTransactionAsync(HelperTools.HexStringToByteArray(txHex2), true, false)));
      }

      await Task.WhenAll(taskList);

      // Need to wait for all nodes to do their calls to mAPI
      NotificationData[] notifications = Array.Empty<NotificationData>();
      while (!notifications.Any())
      {
        notifications = await TxRepositoryPostgres.GetNotificationsForTestsAsync();
        await Task.Delay(500, cts.Token);
      }

      loggerTest.LogInformation("Retrieving notification data");

      Assert.AreEqual(1, notifications.Length);
      Assert.AreEqual(txId2, new uint256(notifications.Single().DoubleSpendTxId).ToString());
    }

    [TestMethod]
    public async Task MultipleInputsWithDS()
    {
      using CancellationTokenSource cts = new(30000);

      // startup another node and link it to the first node
      node1 = StartBitcoind(1, new BitcoindProcess[] { node0 });
      var syncTask = SyncNodesBlocksAsync(cts.Token, node0, node1);

      StartupLiveMAPI();
      var coin0 = availableCoins.Dequeue();
      var coin2 = availableCoins.Dequeue();

      var tx1 = CreateDS_OP_RETURN_Tx(new Coin[] { coin0, availableCoins.Dequeue(), coin2, availableCoins.Dequeue() }, true, 0, 2);
      
      await CheckDsNotifications(tx1, coin0, 1, sendToNode1: true);

      //Create another DS tx which should not trigger another notification
      var (txHex3, txId3) = CreateNewTransaction(coin2, new Money(5000L));

      loggerTest.LogInformation($"Submiting {txId3} with doublespend");
      await Assert.ThrowsExceptionAsync<RpcException>(async () => await node1.RpcClient.SendRawTransactionAsync(HelperTools.HexStringToByteArray(txHex3), true, false));
      await Task.Delay(3000);

      var notifications = await TxRepositoryPostgres.GetNotificationsForTestsAsync();
      Assert.AreEqual(1, notifications.Length);
    }

    [TestMethod]
    public async Task SubmitTxWithMissingCallbackMessage()
    {
      StartupNode1AndLiveMAPI();

      var script = new Script(OpcodeType.OP_FALSE);
      script += OpcodeType.OP_RETURN;
      script += Op.GetPushOp(Encoders.Hex.DecodeData(DSNTIdentifier));

      var coin = availableCoins.Dequeue();
      var tx1 = CreateDS_Tx(coin, script);

      var dsntOutput = tx1.Outputs.Last();
      (bool valid, string warning) = ValidateDsntCallbackMessage(dsntOutput, tx1.Inputs.Count);
      Assert.IsFalse(valid);
      Assert.AreEqual("Missing DSNT callback message.", warning);

      await CheckDsNotifications(tx1, coin, 0, sendToNode1: true);
    }

    [TestMethod]
    public async Task SubmitTxWithInvalidVersion()
    {
      StartupNode1AndLiveMAPI();

      var script = new Script(OpcodeType.OP_FALSE);
      script += OpcodeType.OP_RETURN;
      script += Op.GetPushOp(Encoders.Hex.DecodeData(DSNTIdentifier));

      string dsData = $"ff017f000001{0:D2}"; // ff - reserved bits in version fields should be zero, but are not
      script += Op.GetPushOp(Encoders.Hex.DecodeData(dsData));

      var coin = availableCoins.Dequeue();
      var tx1 = CreateDS_Tx(coin, script);

      var dsntOutput = tx1.Outputs.Last();
      (bool valid, string warning) = ValidateDsntCallbackMessage(dsntOutput, tx1.Inputs.Count);
      Assert.IsFalse(valid);
      Assert.AreEqual("DSNT callback message: invalid version field.", warning);

      await CheckDsNotifications(tx1, coin, 0, sendToNode1: true);
    }

    [TestMethod]
    public async Task SubmitTxWithInvalidZeroIPAddressCount()
    {
      StartupNode1AndLiveMAPI();

      var script = new Script(OpcodeType.OP_FALSE);
      script += OpcodeType.OP_RETURN;
      script += Op.GetPushOp(Encoders.Hex.DecodeData(DSNTIdentifier));

      string dsData = $"01007f000001{0:D2}"; // IP address count = 0
      script += Op.GetPushOp(Encoders.Hex.DecodeData(dsData));

      var coin = availableCoins.Dequeue();
      var tx1 = CreateDS_Tx(coin, script);

      var dsntOutput = tx1.Outputs.Last();
      (bool valid, string warning) = ValidateDsntCallbackMessage(dsntOutput, tx1.Inputs.Count);
      Assert.IsFalse(valid);
      Assert.AreEqual("DSNT callback message: IP address count of 0 is not allowed.", warning);

      await CheckDsNotifications(tx1, coin, 0, sendToNode1: true);
    }

    [TestMethod]
    public async Task SubmitTxWithInvalidIPAddressCount()
    {
      StartupNode1AndLiveMAPI();

      var script = new Script(OpcodeType.OP_FALSE);
      script += OpcodeType.OP_RETURN;
      script += Op.GetPushOp(Encoders.Hex.DecodeData(DSNTIdentifier));
      string dsData = $"01037f0000017f000001{0:D2}"; // IP address count = 3
      script += Op.GetPushOp(Encoders.Hex.DecodeData(dsData));

      var coin = availableCoins.Dequeue();
      var tx1 = CreateDS_Tx(coin, script);

      var dsntOutput = tx1.Outputs.Last();
      (bool valid, string warning) = ValidateDsntCallbackMessage(dsntOutput, tx1.Inputs.Count);
      Assert.IsFalse(valid);
      Assert.AreEqual("DSNT callback message: missing/bad IP address.", warning);

      await CheckDsNotifications(tx1, coin, 0, sendToNode1: true);
    }

    [TestMethod]
    public async Task SubmitTxWithOneValidIPAddress()
    {
      StartupNode1AndLiveMAPI();

      var script = new Script(OpcodeType.OP_FALSE);
      script += OpcodeType.OP_RETURN;
      script += Op.GetPushOp(Encoders.Hex.DecodeData(DSNTIdentifier));
      string dsData = $"01027f0000017f000002{0:D2}"; // IP address count = 2
      script += Op.GetPushOp(Encoders.Hex.DecodeData(dsData));

      var coin = availableCoins.Dequeue();
      var tx1 = CreateDS_Tx(coin, script);

      var dsntOutput = tx1.Outputs.Last();
      (bool valid, _) = ValidateDsntCallbackMessage(dsntOutput, tx1.Inputs.Count);
      Assert.IsTrue(valid);

      await CheckDsNotifications(tx1, coin, 1, sendToNode1: true);
    }

    [TestMethod]
    public async Task SubmitTxWithInvalidIPv6Address()
    {
      StartupNode1AndLiveMAPI();

      var script = new Script(OpcodeType.OP_FALSE);
      script += OpcodeType.OP_RETURN;
      script += Op.GetPushOp(Encoders.Hex.DecodeData(DSNTIdentifier));
      string dsData = $"81037f0000017f000001{0:D2}";
      script += Op.GetPushOp(Encoders.Hex.DecodeData(dsData));

      var coin = availableCoins.Dequeue();
      var tx1 = CreateDS_Tx(coin, script);

      var dsntOutput = tx1.Outputs.Last();
      (bool valid, string warning) = ValidateDsntCallbackMessage(dsntOutput, tx1.Inputs.Count);
      Assert.IsFalse(valid);
      Assert.AreEqual("DSNT callback message: missing/bad IP address.", warning);

      await CheckDsNotifications(tx1, coin, 0, sendToNode1: true);
    }

    [DataRow("7f000001", "7f000002", 1)]
    [DataRow("7f000002", "7f000001", 0)]
    [TestMethod]
    public async Task SubmitTxWithMultipleDsntOutputs(string ipAddress1, string ipAddress2, int expectedDSNotifications)
    {
      // If multiple DSNT outputs, the node only attempts to process the first DSNT output (lowest index) 
      // 7f000001 - localhost is reachable, 7f000002 is not
      StartupNode1AndLiveMAPI();

      var script1 = new Script(OpcodeType.OP_FALSE);
      script1 += OpcodeType.OP_RETURN;
      script1 += Op.GetPushOp(Encoders.Hex.DecodeData(DSNTIdentifier));
      string dsData = $"0101{ipAddress1}{0:D2}";
      script1 += Op.GetPushOp(Encoders.Hex.DecodeData(dsData));

      var script2 = new Script(OpcodeType.OP_FALSE);
      script2 += OpcodeType.OP_RETURN;
      script2 += Op.GetPushOp(Encoders.Hex.DecodeData(DSNTIdentifier));
      string dsData2 = $"0101{ipAddress2}{0:D2}";
      script2 += Op.GetPushOp(Encoders.Hex.DecodeData(dsData2));

      var coin = availableCoins.Dequeue();
      var tx1 = CreateDS_Tx(new Coin[] { coin }, new Script[] { script1, script2 });

      Assert.AreEqual(3, tx1.Outputs.Count);
      (bool valid, _) = ValidateDsntCallbackMessage(tx1.Outputs[1], tx1.Inputs.Count);
      Assert.IsTrue(valid);
      (valid, _) = ValidateDsntCallbackMessage(tx1.Outputs[2], tx1.Inputs.Count);
      Assert.IsTrue(valid);

      await CheckDsNotifications(tx1, coin, expectedDSNotifications, sendToNode1: true);
    }

    [TestMethod]
    public async Task SubmitTxWithMissingInputCount()
    {
      StartupNode1AndLiveMAPI();

      var script = new Script(OpcodeType.OP_FALSE);
      script += OpcodeType.OP_RETURN;
      script += Op.GetPushOp(Encoders.Hex.DecodeData(DSNTIdentifier));
      string dsData = $"01017f000001";
      script += Op.GetPushOp(Encoders.Hex.DecodeData(dsData));

      var coin = availableCoins.Dequeue();
      var tx1 = CreateDS_Tx(coin, script);

      var dsntOutput = tx1.Outputs.Last();
      (bool valid, string warning) = ValidateDsntCallbackMessage(dsntOutput, tx1.Inputs.Count);
      Assert.IsFalse(valid);
      Assert.AreEqual("DSNT callback message: missing input count.", warning);

      await CheckDsNotifications(tx1, coin, 0, sendToNode1: true);
    }

    [TestMethod]
    public async Task SubmitTxWithInvalidInputCount()
    {
      StartupNode1AndLiveMAPI();

      var script = new Script(OpcodeType.OP_FALSE);
      script += OpcodeType.OP_RETURN;
      script += Op.GetPushOp(Encoders.Hex.DecodeData(DSNTIdentifier));
      string dsData = $"01017f000001{2:D2}";
      script += Op.GetPushOp(Encoders.Hex.DecodeData(dsData));

      var coin = availableCoins.Dequeue();
      var tx1 = CreateDS_Tx(coin, script);

      var dsntOutput = tx1.Outputs.Last();
      (bool valid, string warning) = ValidateDsntCallbackMessage(dsntOutput, tx1.Inputs.Count);
      Assert.IsFalse(valid);
      Assert.AreEqual("DSNT callback message: invalid input count.", warning);

      await CheckDsNotifications(tx1, coin, 0, sendToNode1: true);
    }
  }
}
