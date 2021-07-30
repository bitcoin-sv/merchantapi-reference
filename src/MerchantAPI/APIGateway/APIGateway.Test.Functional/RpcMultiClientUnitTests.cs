// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using MerchantAPI.APIGateway.Domain.Models;
using MerchantAPI.APIGateway.Test.Functional.Mock;
using MerchantAPI.Common.BitcoinRpc.Responses;
using MerchantAPI.Common.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MerchantAPI.APIGateway.Test.Functional
{

  /// <summary>
  /// Mock that knows how to return fixed number of nodes.
  /// </summary>
  public class MockNodes : INodes
  {
    List<Node> nodes = new List<Node>();

    public MockNodes(int nrNodes)
    {
      for (int i = 0; i < nrNodes; i++)
      {
        AppendNode(i);
      }
    }

    public Task<Node> CreateNodeAsync(Node node)
    {
      throw new NotImplementedException();
    }

    public int DeleteNode(string id)
    {
      throw new NotImplementedException();
    }

    public Node GetNode(string id)
    {
      throw new NotImplementedException();
    }

    public Node AppendNode(int index)
    {
      var node = new Node(index, "umockNode" + index, 1000 + index, "", "", "", null, (int)NodeStatus.Connected, null, null);
      nodes.Add(node);
      return node;
    }

    public IEnumerable<Node> GetNodes()
    {
      return nodes;
    }

    public Task<bool> UpdateNodeAsync(Node node)
    {
      throw new NotImplementedException();
    }
  }



  [TestClass]
  public class RpcMultiClientUnitTests
  {
    RpcClientFactoryMock rpcClientFactoryMock;
    string txC1Hex = TestBase.txC1Hex;
    string txC1Hash = TestBase.txC1Hash;

    string txC2Hex = TestBase.txC2Hex;
    string txC2Hash = TestBase.txC2Hash;

    string txC3Hex = TestBase.txC3Hex;
    string txC3Hash = TestBase.txC3Hash;


    // Empty response means, that everything was accepted
    RpcSendTransactions okResponse =
      new RpcSendTransactions
      {
        Known = new string[0],
        Evicted = new string[0],

        Invalid = new RpcSendTransactions.RpcInvalidTx[0],
        Unconfirmed = new RpcSendTransactions.RpcUnconfirmedTx[0]
      };

    [TestInitialize]
    public void Initialize()
    {
      rpcClientFactoryMock = new RpcClientFactoryMock();
      rpcClientFactoryMock.AddKnownBlock(0, HelperTools.HexStringToByteArray(TestBase.genesisBlock));
    }

    [TestMethod]
    public async Task GetBlockChainInfoShouldReturnOldestBLock()
    {

      var responses = rpcClientFactoryMock.PredefinedResponse;

      responses.TryAdd("umockNode0:getblockchaininfo",
        new RpcGetBlockchainInfo
        {
          BestBlockHash = "oldest",
          Blocks = 100
        }
      );

      responses.TryAdd("umockNode1:getblockchaininfo",
        new RpcGetBlockchainInfo
        {
          BestBlockHash = "younger",
          Blocks = 101
        }
      );

      var c = new RpcMultiClient(new MockNodes(2), rpcClientFactoryMock, NullLogger<RpcMultiClient>.Instance);

      Assert.AreEqual("oldest", (await c.GetWorstBlockchainInfoAsync()).BestBlockHash);
    }

    [TestMethod]
    public async Task GetFirstSuccessfullNetworkInfo()
    {
      var responses = rpcClientFactoryMock.PredefinedResponse;
      responses.TryAdd("umockNode4:getnetworkinfo", new Exception());

      responses.TryAdd("umockNode3:getnetworkinfo", new Exception());

      responses.TryAdd("umockNode2:getnetworkinfo", new Exception());

      responses.TryAdd("umockNode1:getnetworkinfo", new Exception());

      responses.TryAdd("umockNode0:getnetworkinfo", new RpcGetNetworkInfo
      {
        AcceptNonStdConsolidationInput = true,
        MaxConsolidationInputScriptSize = 10000
      });

      var c = new RpcMultiClient(new MockNodes(5), rpcClientFactoryMock, NullLogger<RpcMultiClient>.Instance);

      for (int i = 0; i < 10; i++)
      {
        var resp = await c.GetAnyNetworkInfoAsync();
        Assert.AreEqual(10000, resp.MaxConsolidationInputScriptSize);
      }
    }


    void ExecuteAndCheckSendTransactions(string[] txsHex, RpcSendTransactions expected, RpcSendTransactions node0Response,
      RpcSendTransactions node1Response)
    {
      rpcClientFactoryMock.SetUpPredefinedResponse(
        ("umockNode0:sendrawtransactions", node0Response),
        ("umockNode1:sendrawtransactions", node1Response));


      var c = new RpcMultiClient(new MockNodes(2), rpcClientFactoryMock, NullLogger<RpcMultiClient>.Instance);

      var r = c.SendRawTransactionsAsync(
        txsHex.Select( x => 
        (HelperTools.HexStringToByteArray(x), true, true, true, new Dictionary<string, object>())).ToArray()).Result;
      Assert.AreEqual(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(r));
    }


    async Task<(RpcGetRawTransaction result, bool allTheSame, Exception error)> ExecuteGetRawTransaction(string txId, RpcGetRawTransaction node0Response, RpcGetRawTransaction node1Response )
    {
      rpcClientFactoryMock.SetUpPredefinedResponse(
        ("umockNode0:getrawtransaction", node0Response),
        ("umockNode1:getrawtransaction", node1Response));


      var c = new RpcMultiClient(new MockNodes(2), rpcClientFactoryMock, NullLogger<RpcMultiClient>.Instance);

      return await c.GetRawTransactionAsync(txId);
    }


    void TestMixedTxC1(RpcSendTransactions node0Response, RpcSendTransactions node1Response)
    {

      ExecuteAndCheckSendTransactions(new[] { txC1Hex }, new RpcSendTransactions
        {
          Known = new string[0],
          Evicted = new string[0],

          Invalid = new[]
          {
            new RpcSendTransactions.RpcInvalidTx()
            {
              Txid = txC1Hash,
              RejectReason = "Mixed results"
            }
          },
          Unconfirmed = new RpcSendTransactions.RpcUnconfirmedTx[0]
        },
        node0Response,
        node1Response);

    }

    [TestMethod]
    public void SendRawTransationsTestMixedResults()
    {

      // Test Known, Evicted combination
      TestMixedTxC1(
        new RpcSendTransactions
        {
          Known = new[]
          {
            txC1Hash
          }
        },

        new RpcSendTransactions
        {
          Evicted = new[]
          {
            txC1Hash
          }
        }
      );

      // Test OK, Evicted
      TestMixedTxC1(
        new RpcSendTransactions(), // Empty results means everything was accepted

        new RpcSendTransactions
        {
          Evicted = new[]
          {
            txC1Hash
          }
        }
      );

      // Test OK, Error combination
      TestMixedTxC1(
        new RpcSendTransactions
        {
          Invalid = new[]
          {
            new RpcSendTransactions.RpcInvalidTx
            {
              Txid = txC1Hash
            }
          }
        },

        new RpcSendTransactions
        {
          Evicted = new[]
          {
            txC1Hash
          }
        }
      );
    }

    [TestMethod]
    public void SendRawTransationsOK()
    {
      ExecuteAndCheckSendTransactions(
        new[] {txC1Hex},
        okResponse,
        okResponse,
        okResponse);
    }

    [TestMethod]
    public void SendRawTransationsWithOneDisconnectedNodeOK()
    {
      // A disconnected node should not affect the result
      rpcClientFactoryMock.DisconnectNode("umockNode0");

      ExecuteAndCheckSendTransactions(
        new[] { txC1Hex },
        okResponse,
        okResponse,
        okResponse);
    }

    [TestMethod]
    public void SendRawTransationsInvalid()
    {
      // Empty response means, that everything was accepted
      var invalidResponse =
        new RpcSendTransactions
        {
          Known = new string[0],
          Evicted = new string[0],

          Invalid = new[]
          {
            new RpcSendTransactions.RpcInvalidTx
            {
              Txid = txC1Hash
            }
          },
          Unconfirmed = new RpcSendTransactions.RpcUnconfirmedTx[0]
        };

      ExecuteAndCheckSendTransactions(
        new [] {txC1Hex},
        invalidResponse,
        invalidResponse,
        invalidResponse);
    }

    [TestMethod]
    public void SendRawTransationsMultiple()
    {

      // txc1 is accepted
      // txc2 is invalid
      // txc3 has mixed result

      // Test Known, Evicted combination
      ExecuteAndCheckSendTransactions(
        new [] { txC1Hex, txC2Hex, txC3Hex },


        new RpcSendTransactions
        {
          Known = new string[0],
          Evicted = new string[0],

          Invalid = new[]
          {
            new RpcSendTransactions.RpcInvalidTx
            {
              Txid = txC2Hash,
              RejectReason = "txc2RejectReason",
              RejectCode =  1
            },
            new RpcSendTransactions.RpcInvalidTx
            {
              Txid = txC3Hash, // tx3 is rejected here
              RejectReason = "Mixed results",
              RejectCode =  null
            }

          },
          Unconfirmed = new RpcSendTransactions.RpcUnconfirmedTx[0]

        },


      new RpcSendTransactions
      {
        Known = new string[0],
        Evicted = new string[0],

        Invalid = new[]
        {
          new RpcSendTransactions.RpcInvalidTx
          {
            Txid = txC2Hash,
            RejectReason = "txc2RejectReason",
            RejectCode =  1
          }
        },
        // tx3 is accepted here (so we do not have it in results)
        Unconfirmed = new RpcSendTransactions.RpcUnconfirmedTx[0]
        
      },


      new RpcSendTransactions
      {
        Known = new string[0],
        Evicted = new string[0],

        Invalid = new[]
        {
          new RpcSendTransactions.RpcInvalidTx
          {
            Txid = txC2Hash
          },
          new RpcSendTransactions.RpcInvalidTx
          {
            Txid = txC3Hash, // tx3 is rejected here
            RejectReason = "txc3RejectReason", // Reason and code get overwritten with Mixed result message
            RejectCode =  1
          }
        },
        Unconfirmed = new RpcSendTransactions.RpcUnconfirmedTx[0]
      });

   }

    [TestMethod]
    public async Task QueryTransactionStatusOK()
    {

      var node0Response = new RpcGetRawTransaction
      {
        Txid = "tx1",
        Blockhash = "b1"
      };

      var node1Response = new RpcGetRawTransaction
      {
        Txid = "tx1",
        Blockhash = "b1"
      };

      var r = await ExecuteGetRawTransaction("tx1", node0Response, node1Response);

      Assert.AreEqual(null, r.error);
      Assert.IsTrue(r.allTheSame);
      Assert.AreEqual("tx1", r.result.Txid);
      Assert.AreEqual("b1", r.result.Blockhash);
    }

    [TestMethod]
    public async Task QueryTransactionStatusWithOneDisconnectedNode()
    {
      
      rpcClientFactoryMock.DisconnectNode("umockNode0");
      var node0Response = new RpcGetRawTransaction
      {
        Txid = "tx1",
        Blockhash = "b1"
      };

      var node1Response = new RpcGetRawTransaction
      {
        Txid = "tx1",
        Blockhash = "b1"
      };

      var r = await ExecuteGetRawTransaction("tx1", node0Response, node1Response);

      Assert.IsNotNull(r.result);
      Assert.IsTrue(r.allTheSame);
      Assert.AreEqual("tx1", r.result.Txid);
      Assert.AreEqual("b1", r.result.Blockhash);

    }


    [TestMethod]
    public async Task QueryTransactionStatusNotConsistent()
    {

      var node0Response = new RpcGetRawTransaction
      {
        Txid = "tx1",
        Blockhash = "b1"
      };

      var node1Response = new RpcGetRawTransaction
      {
        Txid = "tx1",
        Blockhash = "**this*is*some*other*block"
      };

      var r = await ExecuteGetRawTransaction("tx1", node0Response, node1Response);

      Assert.AreEqual(null, r.error);
      Assert.IsFalse(r.allTheSame);
      Assert.IsNull(r.result);
    }

  }
}
