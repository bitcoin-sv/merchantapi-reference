// Copyright(c) 2021 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MerchantAPI.Common.BitcoinRpc.Responses;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MerchantAPI.APIGateway.Test.Functional
{
  [TestCategory("TestCategoryNo1")]
  [TestClass]
  public class NodesWithBitcoindTest : TestBaseWithBitcoind
  {
    [TestInitialize]
    public override void TestInitialize()
    {
      base.TestInitialize();
    }

    [TestCleanup]
    public override void TestCleanup()
    {
      base.TestCleanup();
    }

    [TestMethod]
    [SkipNodeStart]
    public void AddNodeTestZmqEndpoint()
    {
      // if IP is wrong, activeZmqNotifications array is empty
      var ex = Assert.ThrowsException<AggregateException>(() => CreateAndStartNode(0, zmqIp: "unreachable"));
      Assert.AreEqual(
        "Node 'localhost:18332', does not have all required zmq notifications enabled. Missing notifications (pubhashblock,pubdiscardedfrommempool,pubinvalidtx)",
        ex.GetBaseException().Message);

      // if port is wrong, activeZmqNotifications array is not empty
      ex = Assert.ThrowsException<AggregateException>(() => CreateAndStartNode(1, zmqIndex: 200000));
      Assert.AreEqual(
        "Node's ZMQNotification for pubdiscardedfrommempool, pubhashblock, pubinvalidtx: 'tcp://127.0.0.1:228333' is unreachable.",
        ex.GetBaseException().Message);

      // try to replace live node's endpoint with unreachable zmqNotificationsEndpoint
      ex = Assert.ThrowsException<AggregateException>(() => CreateAndStartNode(2, zmqNotificationsEndpoint: "tcp://unreachable"));
      Assert.AreEqual(ex.GetBaseException().Message,
        "ZMQNotificationsEndpoint: 'tcp://unreachable' is unreachable.");
    }

    [TestMethod]
    public void UpdateNodeTestZmqEndpoint()
    {
      // try to replace live node's endpoint "tcp://127.0.0.1:28333" with unreachable zmqNotificationsEndpoint
      var ex = Assert.ThrowsException<AggregateException>(() => UpdateNodeZMQNotificationsEndpoint(0, node0, "tcp://127.0.0.1:28331"));
      Assert.AreEqual(ex.GetBaseException().Message,
        "ZMQNotificationsEndpoint: 'tcp://127.0.0.1:28331' is unreachable.");

      // update node's zmqNotificationsEndpoint to same value is successful
      UpdateNodeZMQNotificationsEndpoint(0, node0, "tcp://127.0.0.1:28333");
    }

    [TestMethod]
    public async Task NodeWarningsBitcoindSettings()
    {
      // test default settings
      var (valid, error, warnings) = await Domain.Models.Nodes.IsNodeValidAsync(rpcClient0, AppSettings);
      Assert.IsTrue(valid);
      Assert.IsNull(error);
      Assert.IsTrue(warnings.Length == 0);

      var defaultParams = new RpcDumpParameters();
      var parameters = await rpcClient0.DumpParametersAsync();
      Assert.AreEqual(defaultParams.RpcServerTimeout, parameters.RpcServerTimeout);
      Assert.AreEqual(defaultParams.MempoolExpiry, parameters.MempoolExpiry);

      Assert.IsTrue(AppSettings.RpcClient.RequestTimeoutSec > parameters.RpcServerTimeout);
      Assert.AreEqual((uint)AppSettings.CleanUpTxAfterMempoolExpiredDays * 24, parameters.MempoolExpiry);

      // test changed settings
      StopBitcoind(node0);
      var args = new List<string>
      {
        "-rpcservertimeout=0",
        "-mempoolexpiry=300"
      };
      node0 = StartBitcoindWithZmq(0, argumentList: args);

      var parametersChanged = await rpcClient0.DumpParametersAsync();
      Assert.AreNotEqual(parameters.RpcServerTimeout, parametersChanged.RpcServerTimeout);
      Assert.AreNotEqual(parameters.MempoolExpiry, parametersChanged.MempoolExpiry);
      (_, _, warnings) = await Domain.Models.Nodes.IsNodeValidAsync(rpcClient0, AppSettings);
      Assert.IsTrue(warnings.Length == 2);

      StopBitcoind(node0);
      // test invalid rpcservertimeout
      args = new List<string>
      {
        "-rpcservertimeout=abc"
      };
      StartBitcoindWithZmq(0, argumentList: args);
      (valid, error, warnings) = await Domain.Models.Nodes.IsNodeValidAsync(rpcClient0, AppSettings);
      Assert.IsFalse(valid);
      Assert.AreEqual("Invalid bitcoind parameters set - check values in RPC dumpparameters.", error);
      Assert.IsTrue(warnings.Length == 0);
    }

    [TestMethod]
    public async Task NodeWarningsDifferentAppSettings()
    {
      var parameters = await rpcClient0.DumpParametersAsync();

      AppSettings.RpcClient.RequestTimeoutSec = 10;
      Assert.IsTrue(AppSettings.RpcClient.RequestTimeoutSec < parameters.RpcServerTimeout);
      var (_, _, warnings) = await Domain.Models.Nodes.IsNodeValidAsync(rpcClient0, AppSettings);
      Assert.AreEqual(
        $"RequestTimeoutSec (value={AppSettings.RpcClient.RequestTimeoutSec}) is smaller than bitcoind's config RpcServerTimeout (value={parameters.RpcServerTimeout}).",
        warnings.Single());

      AppSettings.CleanUpTxAfterMempoolExpiredDays = 10;
      Assert.AreNotEqual(AppSettings.CleanUpTxAfterMempoolExpiredDays * 24, parameters.MempoolExpiry);
      (_, _, warnings) = await Domain.Models.Nodes.IsNodeValidAsync(rpcClient0, AppSettings);
      Assert.AreEqual(2, warnings.Length);

      AppSettings.RpcClient.RequestTimeoutSec = parameters.RpcServerTimeout + 10;
      (_, _, warnings) = await Domain.Models.Nodes.IsNodeValidAsync(rpcClient0, AppSettings);
      Assert.AreEqual(
        $"CleanUpTxAfterMempoolExpiredDays (value=10 days=240 hours) is not in sync with bitcoind's config MempoolExpiry (value=336 hours).",
        warnings.Single());
    }
  }
}
