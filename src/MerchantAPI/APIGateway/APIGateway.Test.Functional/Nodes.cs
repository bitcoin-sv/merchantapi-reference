// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Test.Functional
{

  [TestClass]
  public class Nodes : TestBase
  {

    [TestInitialize]
    public void TestInitialize()
    {
      Initialize(mockedServices: true);
    }

    [TestCleanup]
    public void TestCleanup()
    {
      Cleanup();
    }


    [TestMethod]
    public async Task WhenAddingNewNodeCheckConnectivity()
    {
      //arrange
      // see base.Initialize()
      rpcClientFactoryMock.Reset();

      // Act
      await Nodes.CreateNodeAsync(new Node("node1", 0, "mocked", "mocked", null, null));

      rpcClientFactoryMock.AssertEqualAndClear(
        "node1:getblockcount", "node1:activezmqnotifications");

      // We are able to retrieve a node
      Assert.IsNotNull(Nodes.GetNode("node1:0"));

      rpcClientFactoryMock.DisconnectNode("node2");

      // Node is disconnected, will not be added
      Assert.ThrowsException<AggregateException>(() => Nodes.CreateNodeAsync(new Node("node2", 0, "mocked", "mocked", null, null)).Result);
      rpcClientFactoryMock.AssertEqualAndClear(""); // no successful call was made
      Assert.IsNull(Nodes.GetNode("node2:0"));
    }
  }

}
