// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.Common.BitcoinRpc.Responses;
using MerchantAPI.APIGateway.Domain.Actions;
using MerchantAPI.Common.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NBitcoin;
using NBitcoin.Altcoins;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Test.Functional
{
  [TestCategory("TestCategoryNo1")]
  [TestClass]
  public class ConsolidationTxTest : MapiWithBitcoindTestBase
  {

    protected ConsolidationTxParameters consolidationParameters;

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

    protected enum ConsolidationReason
    {
     None,
     InputMaturity,
     InputScriptSize,
     RatioInOutCount,
     RatioInOutScriptSize
    }

    protected async Task<(string txHex, Transaction txId, PrevOut[] prevOuts)> CreateNewConsolidationTx(ConsolidationReason reason = ConsolidationReason.None)
    {
      var address = BitcoinAddress.Create(testAddress, Network.RegTest);
      var tx = BCash.Instance.Regtest.CreateTransaction();
      Money value = 0L;
      int inCount = 0;
      var OP_NOP_string = "61";
      var key = Key.Parse(testPrivateKeyWif, Network.RegTest);
      int noBlocks = (int)consolidationParameters.MinConfConsolidationInput - 1;

      if (reason == ConsolidationReason.InputMaturity)
      {
        noBlocks--;
      }

      await rpcClient0.GenerateAsync(noBlocks);

      if (reason == ConsolidationReason.InputScriptSize)
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

        if (reason == ConsolidationReason.RatioInOutCount && inCount == consolidationParameters.MinConsolidationFactor - 1)
        {
          break;
        }
      }
      if (reason == ConsolidationReason.RatioInOutScriptSize)
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
      var (_, prevOuts) = await Mapi.CollectPreviousOuputs(tx, null, RpcMultiClient);
      return (tx.ToHex(), tx, prevOuts);
    }

    [TestMethod]
    public virtual async Task SubmitTransactionValid()
    {
      var (txHex, tx, prevOuts) = await CreateNewConsolidationTx();

      Assert.IsTrue(Mapi.IsConsolidationTxn(tx, consolidationParameters, prevOuts));

      var payload = await SubmitTransactionAsync(txHex);

      Assert.AreEqual("success", payload.ReturnResult);

      // Try to fetch tx from the node
      var txFromNode = await rpcClient0.GetRawTransactionAsBytesAsync(tx.GetHash().ToString());

      Assert.AreEqual(txHex, HelperTools.ByteToHexString(txFromNode));
    }

    [TestMethod]
    public virtual async Task SubmitTransactionRatioInOutCount()
    {
      var (txHex, tx, prevOuts) = await CreateNewConsolidationTx(ConsolidationReason.RatioInOutCount);

      Assert.IsFalse(Mapi.IsConsolidationTxn(tx, consolidationParameters, prevOuts));

      var payload = await SubmitTransactionAsync(txHex);

      Assert.AreEqual("failure", payload.ReturnResult);
      Assert.AreEqual("Not enough fees", payload.ResultDescription);
    }

    [TestMethod]
    public virtual async Task SubmitTransactionRatioInOutScript()
    {
      var (txHex, tx, prevOuts) = await CreateNewConsolidationTx(ConsolidationReason.RatioInOutScriptSize);

      Assert.IsFalse(Mapi.IsConsolidationTxn(tx, consolidationParameters, prevOuts));

      var payload = await SubmitTransactionAsync(txHex);

      Assert.AreEqual("failure", payload.ReturnResult);
      Assert.AreEqual("Not enough fees", payload.ResultDescription);
    }

    [TestMethod]
    public virtual async Task SubmitTransactionInputMaturity()
    {
      var (txHex, tx, prevOuts) = await CreateNewConsolidationTx(ConsolidationReason.InputMaturity);
      Assert.IsFalse(Mapi.IsConsolidationTxn(tx, consolidationParameters, prevOuts));

      var payload = await SubmitTransactionAsync(txHex);

      Assert.AreEqual("failure", payload.ReturnResult);
      Assert.AreEqual("Not enough fees", payload.ResultDescription);
    }

    [TestMethod]
    public virtual async Task SubmitTransactionInputScriptSize()
    {
      var (txHex, tx, prevOuts) = await CreateNewConsolidationTx(ConsolidationReason.InputScriptSize);

      Assert.IsFalse(Mapi.IsConsolidationTxn(tx, consolidationParameters, prevOuts));

      var payload = await SubmitTransactionAsync(txHex);

      Assert.AreEqual("failure", payload.ReturnResult);
      Assert.AreEqual("Not enough fees", payload.ResultDescription);
    }

  }
}
