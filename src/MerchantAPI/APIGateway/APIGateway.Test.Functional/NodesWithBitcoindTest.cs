// Copyright(c) 2021 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MerchantAPI.APIGateway.Test.Functional
{
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
  }
}
