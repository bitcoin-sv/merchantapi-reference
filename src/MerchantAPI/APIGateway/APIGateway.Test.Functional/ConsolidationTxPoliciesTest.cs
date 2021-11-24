// Copyright(c) 2021 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain.Actions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Test.Functional
{
  [TestClass]
  public class ConsolidationTxPoliciesTest: ConsolidationTxTest
  {
    ConsolidationTxParameters mergedParameters;

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
    public override async Task SubmitTransactionValid()
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
      await base.SubmitTransactionValid();
    }

    [TestMethod]
    public override async Task SubmitTransactionRatioInOutCount()
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

    [TestMethod]
    public override async Task SubmitTransactionRatioInOutScript()
    {
      var (txHex, tx, prevOuts) = await CreateNewConsolidationTx(ConsolidationReason.RatioInOutScriptSize);

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

    [TestMethod]
    public override async Task SubmitTransactionInputMaturity()
    {
      var (txHex, tx, prevOuts) = await CreateNewConsolidationTx(ConsolidationReason.InputMaturity);
      Assert.IsFalse(Mapi.IsConsolidationTxn(tx, consolidationParameters, prevOuts));

      SetPoliciesForCurrentFeeQuote(
      $"{{" +
      $"\"minconfconsolidationinput\": {consolidationParameters.MinConfConsolidationInput - 1 }" +
      $"}}"
      );
      mergedParameters = FeeQuoteRepository.GetFeeQuoteById(1).GetMergedConsolidationTxParameters(consolidationParameters);
      Assert.IsTrue(Mapi.IsConsolidationTxn(tx, mergedParameters, prevOuts));

      var payload = await SubmitTransactionAsync(txHex);
      Assert.AreEqual("success", payload.ReturnResult);
    }

    [TestMethod]
    public override async Task SubmitTransactionInputScriptSize()
    {
      var (txHex, tx, prevOuts) = await CreateNewConsolidationTx(ConsolidationReason.InputScriptSize);

      Assert.IsFalse(Mapi.IsConsolidationTxn(tx, consolidationParameters, prevOuts));

      SetPoliciesForCurrentFeeQuote(
      $"{{" +
      $"\"maxconsolidationinputscriptsize\": {consolidationParameters.MaxConsolidationInputScriptSize + 1}, " +
      "\"acceptnonstdconsolidationinput\": true " +
      $"}}"
      );
      mergedParameters = FeeQuoteRepository.GetFeeQuoteById(1).GetMergedConsolidationTxParameters(consolidationParameters);
      Assert.IsTrue(Mapi.IsConsolidationTxn(tx, mergedParameters, prevOuts));

      var payload = await SubmitTransactionAsync(txHex);
      Assert.AreEqual("failure", payload.ReturnResult);
      Assert.AreEqual("16 mandatory-script-verify-flag-failed (Only non-push operators allowed in signatures)", payload.ResultDescription);
    }
  }
}
