// Copyright(c) 2021 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain;
using MerchantAPI.APIGateway.Domain.ViewModels;
using MerchantAPI.APIGateway.Rest.ViewModels;
using MerchantAPI.APIGateway.Test.Functional.Server;
using MerchantAPI.Common.BitcoinRpc.Responses;
using MerchantAPI.Common.Json;
using MerchantAPI.Common.Test.Clock;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Test.Functional
{
  public class MapiWithBitcoindTestBase : TestBaseWithBitcoind
  {
    protected int cancellationTimeout = 30000; // 30 seconds

    [TestInitialize]
    public override void TestInitialize()
    {
      base.TestInitialize();
      InsertFeeQuote();
    }


    [TestCleanup]
    public override void TestCleanup()
    {
      base.TestCleanup();
    }

    protected Transaction CreateNewTransactionTx(long satoshis = 1000L)
    {
      // Create transaction from a coin 
      var coin = availableCoins.Dequeue();
      var amount = new Money(satoshis);
      return CreateNewTransactionTx(coin, amount);
    }

    protected (string txHex, string txId) CreateNewTransaction(long satoshis = 1000L, int numOfOutputs = 1)
    {
      // Create transaction from coins
      Coin[] coins = new Coin[numOfOutputs];
      for (int i = 0; i < numOfOutputs; i++)
      {
        coins[i] = availableCoins.Dequeue();
      }
      var amount = new Money(satoshis);
      return CreateNewTransaction(coins, amount);
    }

    public (string txHex, string txId) CreateNewTransactionWithData(Transaction tx0)
    {
      // Create transaction from a coin 
      var coin = availableCoins.Dequeue();

      long txLength = 110000;
      long dataLength = 0;
      long standard = txLength - dataLength;
      var fee = FeeQuoteRepository.GetValidFeeQuotesByIdentity(null).Single().Fees.Single(x => x.FeeType == Const.FeeType.Data);
      var minRequiredFees = Math.Min((dataLength * fee.RelayFee.Satoshis) / fee.RelayFee.Bytes, // 0
                    (dataLength * fee.MiningFee.Satoshis) / fee.MiningFee.Bytes); 
      fee = FeeQuoteRepository.GetValidFeeQuotesByIdentity(null).Single().Fees.Single(x => x.FeeType == Const.FeeType.Standard);
      minRequiredFees += Math.Min((standard * fee.RelayFee.Satoshis) / fee.RelayFee.Bytes, 
              (standard * fee.MiningFee.Satoshis) / fee.MiningFee.Bytes); // 27500


      var tx1 = CreateTransaction(tx0, txLength, dataLength, minRequiredFees, coin); 

      var key1 = Key.Parse(testPrivateKeyWif, Network.RegTest);

      tx1.Sign(key1.GetBitcoinSecret(Network.RegTest), coin);

      return (tx1.ToHex(), tx1.GetHash().ToString());
    }

    public async Task<SubmitTransactionResponseViewModel> SubmitTransactionAsync(string txHex, bool merkleProof = false, bool dsCheck = false, string merkleFormat = "",
          HttpStatusCode expectedHttpStatusCode = HttpStatusCode.OK, string expectedHttpMessage = null)
    {
      // Send transaction
      var reqContent = GetJsonRequestContent(txHex, merkleProof, dsCheck, merkleFormat);

      var (response, message) =
        await Post<SignedPayloadViewModel>(MapiServer.ApiMapiSubmitTransaction, Client, reqContent, expectedHttpStatusCode);

      await CheckHttpResponseMessageDetailAsync(message, expectedHttpMessage);

      return response?.ExtractPayload<SubmitTransactionResponseViewModel>();
    }

    public async Task<SubmitTransactionsResponseViewModel> SubmitTransactionsAsync(string[] txHexList, bool dsCheck = false, bool merkleProof = false)
    {

      // Send transaction

      var reqJSON = "[{\"rawtx\": \"" + string.Join("\"}, {\"rawtx\": \"", txHexList) + "\"}]";
      var reqContent = new StringContent(reqJSON);
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);

      string url = MapiServer.ApiMapiSubmitTransactions;
      if (dsCheck)
      {
        List<(string, string)> queryParams = new()
        {
          ("defaultDsCheck", dsCheck.ToString()),
          ("defaultCallbackUrl", "https://test.domain")
        };
        url = PrepareQueryParams(url, queryParams);
      }
      if (merkleProof)
      {
        List<(string, string)> queryParams = new()
        {
          ("defaultMerkleProof", merkleProof.ToString()),
          ("defaultCallbackUrl", Common.Test.CallbackFunctionalTests.Url)
        };
        url = PrepareQueryParams(url, queryParams);
      }

      var response =
        await Post<SignedPayloadViewModel>(url, Client, reqContent, HttpStatusCode.OK);

      return response.response.ExtractPayload<SubmitTransactionsResponseViewModel>();
    }

    protected async Task<QueryTransactionStatusResponseViewModel> QueryTransactionStatus(string txId, bool? merkleProof=null, string merkleFormat=null)
    {
      List<(string, string)> queryParams = new();
      if (merkleProof != null)
      {
        queryParams.Add(("merkleProof", merkleProof.Value.ToString()));
      }
      if (merkleFormat != null)
      {
        queryParams.Add(("merkleFormat", merkleFormat));
      }

      var url = PrepareQueryParams(MapiServer.ApiMapiQueryTransactionStatus + txId, queryParams);
      var response = await Get<SignedPayloadViewModel>(
        Client, url, HttpStatusCode.OK);

      return response.ExtractPayload<QueryTransactionStatusResponseViewModel>();
    }

    protected async Task AssertQueryTxAsync(
      QueryTransactionStatusResponseViewModel response,
      string expectedTxId,
      string expectedResult = "success",
      string expectedDescription = null,
      long? confirmations = null,
      string checkMerkleProofWithMerkleFormat = null,
      int txStatus = TxStatus.Accepted,
      bool checkBestBlock = true)
    {
      Assert.AreEqual(Const.MERCHANT_API_VERSION, response.ApiVersion);
      Assert.IsTrue((MockedClock.UtcNow - response.Timestamp).TotalSeconds < 60);
      Assert.AreEqual(expectedTxId, response.Txid);
      Assert.AreEqual(expectedResult, response.ReturnResult);
      Assert.AreEqual(expectedDescription, response.ResultDescription);

      Assert.AreEqual(MinerId.GetCurrentMinerIdAsync().Result, response.MinerId);
      Assert.AreEqual(confirmations, response.Confirmations);

      if (expectedResult == "success")
      {
        if (confirmations != null)
        {
          if (checkBestBlock)
          {
            var blockChainInfo = await BlockChainInfo.GetInfoAsync();
            Assert.AreEqual(blockChainInfo.BestBlockHeight, response.BlockHeight);
            Assert.AreEqual(blockChainInfo.BestBlockHash, response.BlockHash);
          }
          if (checkMerkleProofWithMerkleFormat == null)
          {
            Assert.IsNull(response.MerkleProof);
          }
          else
          {
            Assert.IsNotNull(response.MerkleProof);
            var jsonMerkleProof = ((JsonElement)response.MerkleProof).GetRawText();
            if (checkMerkleProofWithMerkleFormat == MerkleFormat.TSC)
            {
              RpcGetMerkleProof2 merkleProof2 = JsonSerializer.Deserialize<RpcGetMerkleProof2>(jsonMerkleProof);
              Assert.AreEqual(response.Txid, merkleProof2.TxOrId);
            }
            else
            {
              RpcGetMerkleProof merkleProof = JsonSerializer.Deserialize<RpcGetMerkleProof>(jsonMerkleProof);
              Assert.AreEqual(response.Txid, merkleProof.TxOrId);
              Assert.AreEqual(response.BlockHash, merkleProof.Target.Hash);
            }
          }
        }
        else
        {
          Assert.IsNull(response.Confirmations);
          Assert.IsNull(response.MerkleProof);
          Assert.IsNull(response.BlockHash);
          Assert.IsNull(response.BlockHeight);
        }
        Assert.AreEqual(0, response.TxSecondMempoolExpiry);
        await AssertTxStatus(response.Txid, txStatus);
      }
      else
      {
        await AssertTxStatus(response.Txid, TxStatus.NotPresentInDb);
      }
    }


    protected async Task<(string, string, int)> CreateUnconfirmedAncestorChainAsync(
      string txHex1, string txId1, int length, int sendToMAPIRate, bool sendLastToMAPI = false, CancellationToken? cancellationToken = null, HttpStatusCode expectedCode = HttpStatusCode.OK)
    {
      var curTxHex = txHex1;
      var curTxId = txId1;
      var mapiTxCount = 0;
      for (int i = 0; i < length; i++)
      {
        Transaction.TryParse(curTxHex, Network.RegTest, out Transaction curTx);
        var curTxCoin = new Coin(curTx, 0);
        (curTxHex, curTxId) = CreateNewTransaction(curTxCoin, new Money(1000L));

        // Submit every sendToMAPIRate tx to mapi with dsCheck
        if ((sendToMAPIRate != 0 && i % sendToMAPIRate == 0) || (sendLastToMAPI && i == length - 1))
        {
          var payload = await SubmitTransactionAsync(curTxHex, true, true, expectedHttpStatusCode: expectedCode);
          if (expectedCode == HttpStatusCode.OK)
          {
            Assert.AreEqual(payload.ReturnResult, "success");
          }
          mapiTxCount++;
        }
        else
        {
          _ = await node0.RpcClient.SendRawTransactionAsync(HelperTools.HexStringToByteArray(curTxHex), true, false, cancellationToken);
        }
      }

      return (curTxHex, curTxId, mapiTxCount);
    }

    protected async Task AddNodeAndWait(BitcoindProcess node, BitcoindProcess addNode, int currentConnectionCount, bool syncNodes = true, CancellationToken cancellationToken = default)
    {
      await node.RpcClient.AddNodeAsync(addNode.Host, addNode.P2Port);

      while (await node.RpcClient.GetConnectionCountAsync(cancellationToken) == currentConnectionCount)
      {
        await Task.Delay(100, cancellationToken);
      }

      if (syncNodes)
      {
        await SyncNodesBlocksAsync(cancellationToken, addNode, node);
      }
    }

    protected async Task DisconnectNodeAndWait(BitcoindProcess node, BitcoindProcess addNode, int currentConnectionCount, CancellationToken cancellationToken = default)
    {
      await node.RpcClient.DisconnectNodeAsync(addNode.Host, addNode.P2Port);

      while (await node.RpcClient.GetConnectionCountAsync(cancellationToken) > (currentConnectionCount - 1))
      {
        await Task.Delay(100, cancellationToken);
      }
    }
  }
}
