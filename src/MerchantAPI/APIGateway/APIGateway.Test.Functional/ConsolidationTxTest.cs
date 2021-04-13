// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.Common.BitcoinRpc.Responses;
using MerchantAPI.APIGateway.Domain.Actions;
using MerchantAPI.APIGateway.Domain.ViewModels;
using MerchantAPI.APIGateway.Rest.ViewModels;
using MerchantAPI.APIGateway.Test.Functional.Server;
using MerchantAPI.Common.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NBitcoin;
using NBitcoin.Altcoins;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Test.Functional
{

  [TestClass]
  public class ConsolidationTxTest : TestBaseWithBitcoind
  {

    ConsolidationTxParameters consolidationParameters;

    [TestInitialize]
    public override void TestInitialize()
    {
      base.TestInitialize();
      InsertFeeQuote();

      //make additional 10 coins
      foreach (var coin in GetCoins(base.rpcClient0, 10))
      {
        availableCoins.Enqueue(coin);
      }

      consolidationParameters = new ConsolidationTxParameters(rpcClient0.GetNetworkInfoAsync().Result);
    }

    [TestCleanup]
    public override void TestCleanup()
    {
      base.TestCleanup();
    }


    async Task<(string txHex, Transaction txId, PrevOut[] prevOuts)> CreateNewConsolidationTx(bool valid = true, string reason = "")
    {
      var address = BitcoinAddress.Create(testAddress, Network.RegTest);
      var tx = BCash.Instance.Regtest.CreateTransaction();
      Money value = 0L;
      int inCount = 0;
      var OP_NOP_string = "61";
      var key = Key.Parse(testPrivateKeyWif, Network.RegTest);
      int noBlocks = (int)consolidationParameters.MinConsolidationInputMaturity - 1;

      if (reason == "inputMaturity")
      {
        noBlocks--;
      }

      await rpcClient0.GenerateAsync(noBlocks);

      if (reason == "inputScriptSize")
      {
        Coin coin = availableCoins.Dequeue();
        tx.Inputs.Add(new TxIn(coin.Outpoint));
        tx.Sign(key.GetBitcoinSecret(Network.RegTest), coin);

        var sig = tx.Inputs[0].ScriptSig;
        string[] arr = new string[(int)consolidationParameters.MaxConsolidationInputScriptSize + 1 - sig.Length];
        Array.Fill(arr, OP_NOP_string);
        var sighex = string.Concat(arr) + sig.ToHex();
        tx.Inputs[0] = new TxIn(coin.Outpoint, Script.FromHex(sighex));

        value += coin.Amount;
        inCount++;
      }

      foreach (Coin coin in availableCoins)
      {
        tx.Inputs.Add(new TxIn(coin.Outpoint));

        value += coin.Amount;
        inCount++;

        if (reason == "ratioInOutCount" && inCount == consolidationParameters.MinConsolidationFactor - 1)
        {
          break;
        }
      }
      if (reason == "ratioInOutScriptSize")
      {
        string coinPubKey = availableCoins.ElementAt(0).ScriptPubKey.ToHex();
        tx.Outputs.Add(value, Script.FromHex(OP_NOP_string + coinPubKey));
      }
      else
      {
        tx.Outputs.Add(value, address);
      }
      tx.Sign(key.GetBitcoinSecret(Network.RegTest), availableCoins);

      var spendOutputs = tx.Inputs.Select(x => (txId: x.PrevOut.Hash.ToString(), N: (long)x.PrevOut.N)).ToArray();
      var (_, prevOuts) = await Mapi.CollectPreviousOuputs(tx, null, rpcMultiClient);
      return (tx.ToHex(), tx, prevOuts);
    }


    async Task<SubmitTransactionResponseViewModel> SubmitTransaction(string txHex)
    {
      // Send transaction
      var reqContent = new StringContent($"{{ \"rawtx\": \"{txHex}\" }}");
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);

      var response =
        await Post<SignedPayloadViewModel>(MapiServer.ApiMapiSubmitTransaction, client, reqContent, HttpStatusCode.OK);

      return response.response.ExtractPayload<SubmitTransactionResponseViewModel>();
    }

    [TestMethod]
    public async Task SubmitTransactionValid()
    {
      var (txHex, tx, prevOuts) = await CreateNewConsolidationTx();

      Assert.IsTrue(Mapi.IsConsolidationTxn(tx, consolidationParameters, prevOuts));

      var payload = await SubmitTransaction(txHex);

      Assert.AreEqual("success", payload.ReturnResult);

      // Try to fetch tx from the node
      var txFromNode = await rpcClient0.GetRawTransactionAsBytesAsync(tx.GetHash().ToString());

      Assert.AreEqual(txHex, HelperTools.ByteToHexString(txFromNode));
    }

    [TestMethod]
    public async Task SubmitTransactionRatioInOutCount()
    {
      var (txHex, tx, prevOuts) = await CreateNewConsolidationTx(false, "ratioInOutCount");

      Assert.IsFalse(Mapi.IsConsolidationTxn(tx, consolidationParameters, prevOuts));

      var payload = await SubmitTransaction(txHex);

      Assert.AreEqual("failure", payload.ReturnResult);
      Assert.AreEqual("Not enough fees", payload.ResultDescription);
    }

    [TestMethod]
    public async Task SubmitTransactionRatioInOutScript()
    {
      var (txHex, tx, prevOuts) = await CreateNewConsolidationTx(false, "ratioInOutScriptSize");

      Assert.IsFalse(Mapi.IsConsolidationTxn(tx, consolidationParameters, prevOuts));

      var payload = await SubmitTransaction(txHex);

      Assert.AreEqual("failure", payload.ReturnResult);
      Assert.AreEqual("Not enough fees", payload.ResultDescription);
    }

    [TestMethod]
    public async Task SubmitTransactionInputMaturity()
    {
      var (txHex, tx, prevOuts) = await CreateNewConsolidationTx(false, "inputMaturity");
      Assert.IsFalse(Mapi.IsConsolidationTxn(tx, consolidationParameters, prevOuts));

      var payload = await SubmitTransaction(txHex);

      Assert.AreEqual("failure", payload.ReturnResult);
      Assert.AreEqual("Not enough fees", payload.ResultDescription);
    }

    [TestMethod]
    public async Task SubmitTransactionInputScriptSize()
    {
      var (txHex, tx, prevOuts) = await CreateNewConsolidationTx(false, "inputScriptSize");

      Assert.IsFalse(Mapi.IsConsolidationTxn(tx, consolidationParameters, prevOuts));

      var payload = await SubmitTransaction(txHex);

      Assert.AreEqual("failure", payload.ReturnResult);
      Assert.AreEqual("Not enough fees", payload.ResultDescription);
    }

  }
}
