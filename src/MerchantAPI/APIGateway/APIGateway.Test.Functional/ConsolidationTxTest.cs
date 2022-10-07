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
    ConsolidationTxParameters mergedParameters;

    [TestInitialize]
    public override void TestInitialize()
    {
      base.TestInitialize();

      GenerateCoins();
    }

    private void GenerateCoins()
    {
      if (base.rpcClient0 != null)
      {
        //make additional 10 coins
        foreach (var coin in GetCoins(base.rpcClient0, 10))
        {
          availableCoins.Enqueue(coin);
        }

        consolidationParameters = new ConsolidationTxParameters(rpcClient0.GetNetworkInfoAsync().Result);
      }
    }

    [TestCleanup]
    public override void TestCleanup()
    {
      base.TestCleanup();
    }

    protected enum ConsolidationReason
    {
     None,
     InputMinConf,
     InputScriptSize,
     InputNonStd,
     RatioInOutCount,
     RatioInOutScriptSize
    }

    protected async Task<(string txHex, Transaction txId, PrevOut[] prevOuts)> CreateNewConsolidationTx(ConsolidationReason reason = ConsolidationReason.None, int? generateBlocks = null)
    {
      var address = BitcoinAddress.Create(testAddress, Network.RegTest);
      var tx = BCash.Instance.Regtest.CreateTransaction();
      Money value = 0L;
      int inCount = 0;
      var OP_NOP_string = "61";
      var key = Key.Parse(testPrivateKeyWif, Network.RegTest);
      int noBlocks = generateBlocks ?? (int)consolidationParameters.MinConfConsolidationInput - 1;

      if (reason == ConsolidationReason.InputMinConf)
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
      if (reason == ConsolidationReason.InputNonStd)
      {
        prevOuts[0].IsStandard = false;
      }
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
    public async Task SubmitTransactionValidWithPolicy()
    {
      var (txHex, tx, prevOuts) = await CreateNewConsolidationTx();

      // set too high MinConfConsolidationInput
      SetPoliciesForCurrentFeeQuote(
      $"{{" +
      $"\"minconfconsolidationinput\": {consolidationParameters.MinConfConsolidationInput + 1} " +
      $"}}"
      );
      mergedParameters = FeeQuoteRepository.GetFeeQuoteById(1).GetMergedConsolidationTxParameters(consolidationParameters);
      Assert.IsFalse(Mapi.IsConsolidationTxn(tx, mergedParameters, prevOuts));

      var payload = await SubmitTransactionAsync(txHex);

      Assert.AreEqual("failure", payload.ReturnResult);

      // clear policies
      SetPoliciesForCurrentFeeQuote(null);
      // submit should now succeed
      await SubmitTransactionValid();
    }

    [DataRow(ConsolidationReason.RatioInOutCount)]
    [DataRow(ConsolidationReason.RatioInOutScriptSize)]
    [DataRow(ConsolidationReason.InputMinConf)]
    [DataRow(ConsolidationReason.InputScriptSize)]
    [TestMethod]
    public async Task CheckConsolidationTransaction(int consolidationReason)
    {
      var (txHex, tx, prevOuts) = await CreateNewConsolidationTx((ConsolidationReason)consolidationReason);

      Assert.IsFalse(Mapi.IsConsolidationTxn(tx, consolidationParameters, prevOuts));

      var payload = await SubmitTransactionAsync(txHex);

      Assert.AreEqual("failure", payload.ReturnResult);
      Assert.AreEqual("Not enough fees", payload.ResultDescription);
    }

    [TestMethod]
    public async Task SubmitTransactionRatioInOutCount()
    {
      var (txHex, tx, prevOuts) = await CreateNewConsolidationTx(ConsolidationReason.RatioInOutCount);

      Assert.IsFalse(Mapi.IsConsolidationTxn(tx, consolidationParameters, prevOuts));

      SetPoliciesForCurrentFeeQuote(
      $"{{" +
      $"\"minconsolidationfactor\": {consolidationParameters.MinConsolidationFactor - 1} " +
      $"}}"
      );
      mergedParameters = FeeQuoteRepository.GetFeeQuoteById(1).GetMergedConsolidationTxParameters(consolidationParameters);
      Assert.IsTrue(Mapi.IsConsolidationTxn(tx, mergedParameters, prevOuts));

      var payload = await SubmitTransactionAsync(txHex);
      Assert.AreEqual("success", payload.ReturnResult);
    }

    [DataRow(ConsolidationReason.RatioInOutCount)]
    [DataRow(ConsolidationReason.RatioInOutScriptSize)]
    [DataRow(ConsolidationReason.InputMinConf)]
    [DataRow(ConsolidationReason.InputScriptSize, "16 mandatory-script-verify-flag-failed (Only non-push operators allowed in signatures)")]
    [TestMethod]
    public async Task CheckConsolidationTransactionWithPolicy(int consolidationReason, string errorDescription = null)
    {
      var (txHex, tx, prevOuts) = await CreateNewConsolidationTx((ConsolidationReason)consolidationReason);

      Assert.IsFalse(Mapi.IsConsolidationTxn(tx, consolidationParameters, prevOuts));

      string policy = null;
      switch ((ConsolidationReason)consolidationReason)
      {
        case ConsolidationReason.RatioInOutCount:
        case ConsolidationReason.RatioInOutScriptSize:
          policy = $"\"minconsolidationfactor\": {consolidationParameters.MinConsolidationFactor - 1} ";
          break;
        case ConsolidationReason.InputMinConf:
          policy = $"\"minconfconsolidationinput\": {consolidationParameters.MinConfConsolidationInput - 1 }";
          break;
        case ConsolidationReason.InputScriptSize:
          policy = $"\"maxconsolidationinputscriptsize\": {consolidationParameters.MaxConsolidationInputScriptSize + 1}, " +
      "\"acceptnonstdconsolidationinput\": true ";
          break;
      }

      SetPoliciesForCurrentFeeQuote($"{{ { policy }}}");

      mergedParameters = FeeQuoteRepository.GetFeeQuoteById(1).GetMergedConsolidationTxParameters(consolidationParameters);
      Assert.IsTrue(Mapi.IsConsolidationTxn(tx, mergedParameters, prevOuts));

      var payload = await SubmitTransactionAsync(txHex);
      if (errorDescription == null)
      {
        Assert.AreEqual("success", payload.ReturnResult);
      }
      else
      {
        Assert.AreEqual("failure", payload.ReturnResult);
        Assert.AreEqual(errorDescription, payload.ResultDescription);
      }
    }

    [TestMethod]
    public async Task CheckConsolidationTransactionInputNonStd()
    {
      var (_, tx, prevOuts) = await CreateNewConsolidationTx(ConsolidationReason.InputNonStd);

      Assert.IsFalse(Mapi.IsConsolidationTxn(tx, consolidationParameters, prevOuts));

      SetPoliciesForCurrentFeeQuote(
      $"{{" +
      "\"acceptnonstdconsolidationinput\": false " +
      $"}}"
      );
      mergedParameters = FeeQuoteRepository.GetFeeQuoteById(1).GetMergedConsolidationTxParameters(consolidationParameters);
      Assert.IsFalse(Mapi.IsConsolidationTxn(tx, mergedParameters, prevOuts));

      SetPoliciesForCurrentFeeQuote(
      $"{{" +
      "\"acceptnonstdconsolidationinput\": true " +
      $"}}"
      );
      mergedParameters = FeeQuoteRepository.GetFeeQuoteById(1).GetMergedConsolidationTxParameters(consolidationParameters);
      Assert.IsTrue(Mapi.IsConsolidationTxn(tx, mergedParameters, prevOuts));
    }

    [DataRow(0, 5, 5)]
    [DataRow(3, 0, 3)]
    [DataRow(0, 0, 6)]
    [SkipNodeStart]
    [TestMethod]
    public async Task CheckConsolidationTransactionWithZeroConfirmations(int setNodeValue, int setPolicyValue, int expectedMergedValue)
    {
      // bitcoind treats value 0 as valid, but ignores it if set as startup argument or with policy
      var node0 = CreateAndStartNode(0, argumentList: new() { $"-minconfconsolidationinput={setNodeValue}" });
      var networkInfo = await node0.RpcClient.GetNetworkInfoAsync();
      Assert.AreEqual(setNodeValue == 0 ? 6 : setNodeValue, networkInfo.MinConfConsolidationInput);

      rpcClient0 = node0.RpcClient;
      SetupChain(rpcClient0);
      GenerateCoins();
      var (txHex, tx, prevOuts) = await CreateNewConsolidationTx(ConsolidationReason.InputMinConf, expectedMergedValue);
      SetPoliciesForCurrentFeeQuote($"{{ {$"\"minconfconsolidationinput\": {setPolicyValue}"}}}");

      mergedParameters = FeeQuoteRepository.GetFeeQuoteById(1).GetMergedConsolidationTxParameters(consolidationParameters);
      Assert.AreEqual(expectedMergedValue, mergedParameters.MinConfConsolidationInput);
      Assert.IsTrue(Mapi.IsConsolidationTxn(tx, mergedParameters, prevOuts));

      var payload = await SubmitTransactionAsync(txHex);
      Assert.AreEqual("success", payload.ReturnResult);
    }
  }
}
