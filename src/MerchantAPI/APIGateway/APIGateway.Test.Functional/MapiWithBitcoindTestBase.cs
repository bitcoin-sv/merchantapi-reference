// Copyright(c) 2021 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain;
using MerchantAPI.APIGateway.Domain.ViewModels;
using MerchantAPI.APIGateway.Rest.ViewModels;
using MerchantAPI.APIGateway.Test.Functional.Server;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NBitcoin;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
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


      var tx1 = CreateTransaction(tx0, txLength, dataLength, minRequiredFees, coin); // submit tx1 should succeed

      var key1 = Key.Parse(testPrivateKeyWif, Network.RegTest);

      tx1.Sign(key1.GetBitcoinSecret(Network.RegTest), coin);

      return (tx1.ToHex(), tx1.GetHash().ToString());
    }

    public async Task<SubmitTransactionResponseViewModel> SubmitTransactionAsync(string txHex, bool merkleProof = false, bool dsCheck = false, string merkleFormat = "")
    {

      // Send transaction
      var reqContent = GetRequestContent(txHex, merkleProof, dsCheck, merkleFormat);

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

    StringContent GetRequestContent(string txHexSingleOrMultiple, bool merkleProof = false, bool dsCheck = false, string merkleFormat = "")
    {
      var reqContent = new StringContent(

        merkleProof || dsCheck ?
          $"{{ \"rawtx\": \"{txHexSingleOrMultiple}\", \"merkleProof\": {merkleProof.ToString().ToLower()}, \"merkleFormat\": \"{merkleFormat}\", \"dsCheck\": {dsCheck.ToString().ToLower()}, \"callbackUrl\" : \"{Callback.Url}\"}}"
          :
          $"{{ \"rawtx\": \"{txHexSingleOrMultiple}\" }}"
        );
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);
      return reqContent;
    }

    protected async Task<QueryTransactionStatusResponseViewModel> QueryTransactionStatus(string txId)
    {
      var response = await Get<SignedPayloadViewModel>(
        client, MapiServer.ApiMapiQueryTransactionStatus + txId, HttpStatusCode.OK);

      return response.ExtractPayload<QueryTransactionStatusResponseViewModel>();
    }
  }
}
