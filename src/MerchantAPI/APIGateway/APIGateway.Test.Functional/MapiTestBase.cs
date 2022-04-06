// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain.ViewModels;
using MerchantAPI.APIGateway.Rest.ViewModels;
using MerchantAPI.APIGateway.Test.Functional.Attributes;
using MerchantAPI.Common.Test.Clock;
using MerchantAPI.Common.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using MerchantAPI.APIGateway.Domain;

namespace MerchantAPI.APIGateway.Test.Functional
{
  public class MapiTestBase : TestBase
  {
    public TestContext TestContext { get; set; }
    protected string CallbackIPaddresses = "127.0.0.1";

    [TestInitialize]
    virtual public void TestInitialize()
    {
      //Retrive OverrideSettingAttribute data (setting name and value)
      List<KeyValuePair<string, string>> overridenSettings = new();
      var overrideSettingsAttributes = GetType().GetMethod(TestContext.TestName).GetCustomAttributes(true).Where(a => a.GetType() == typeof(OverrideSettingAttribute));
      foreach (var attribute in overrideSettingsAttributes)
      {
        OverrideSettingAttribute overrideSettingsAttribute = (OverrideSettingAttribute)attribute;
        overridenSettings.Add(new KeyValuePair<string, string>(overrideSettingsAttribute.SettingName, overrideSettingsAttribute.SettingValue.ToString()));
      }

      base.Initialize(mockedServices: true, overridenSettings);
      AddMockNode(0);
    }

    [TestCleanup]
    virtual public void TestCleanup()
    {
      Cleanup();
    }

    protected static void VerifySignature((SignedPayloadViewModel response, HttpResponseMessage httpResponse) response)
    {
      Assert.IsTrue(JsonEnvelopeSignature.VerifySignature(response.response.ToDomainObject()), "Signature is invalid");
    }

    protected async Task AssertIsOKAsync(FeeQuoteViewModelGet response)
    {

      Assert.AreEqual("1.4.0", response.ApiVersion);
      Assert.IsTrue((MockedClock.UtcNow - response.Timestamp).TotalSeconds < 60);

      Assert.AreEqual(MinerId.GetCurrentMinerIdAsync().Result, response.MinerId);
      var blockChainInfo = await BlockChainInfo.GetInfoAsync();
      Assert.AreEqual(blockChainInfo.BestBlockHeight, response.CurrentHighestBlockHeight);
      Assert.AreEqual(blockChainInfo.BestBlockHash, response.CurrentHighestBlockHash);
      Assert.AreEqual(CallbackIPaddresses,
                response.Callbacks != null ? String.Join(",", response.Callbacks.Select(x => x.IPAddress)) : null);
    }

    protected async Task AssertIsOKAsync(SubmitTransactionResponseViewModel response, string expectedTxId, string expectedResult = "success", string expectedDescription = "")
    {
      // typical flow for successful submit
      if (expectedResult == "success")
      {
        await AssertIsOKAsync(response, expectedTxId, TxStatus.Mempool, expectedResult, expectedDescription);
      }
      else
      {
        await AssertIsOKAsync(response, expectedTxId, TxStatus.NotPresentInDb, expectedResult, expectedDescription);
      }
    }

    protected async Task AssertIsOKAsync(SubmitTransactionResponseViewModel response, string expectedTxId, int txStatus, string expectedResult = "success", string expectedDescription = "")
    {
      Assert.AreEqual("1.4.0", response.ApiVersion);
      Assert.IsTrue((MockedClock.UtcNow - response.Timestamp).TotalSeconds < 60);
      Assert.AreEqual(expectedResult, response.ReturnResult);
      Assert.AreEqual(expectedDescription, response.ResultDescription);

      Assert.AreEqual(MinerId.GetCurrentMinerIdAsync().Result, response.MinerId);
      var blockChainInfo = await BlockChainInfo.GetInfoAsync();
      Assert.AreEqual(blockChainInfo.BestBlockHeight, response.CurrentHighestBlockHeight);
      Assert.AreEqual(blockChainInfo.BestBlockHash, response.CurrentHighestBlockHash);
      Assert.AreEqual(expectedTxId, response.Txid);

      await AssertTxStatus(response.Txid, txStatus);
    }

    protected async Task ValidateHeaderSubmitTransactionsAsync(SubmitTransactionsResponseViewModel response)
    {
      // validate header
      Assert.AreEqual("1.4.0", response.ApiVersion);
      Assert.IsTrue((MockedClock.UtcNow - response.Timestamp).TotalSeconds < 60);
      Assert.AreEqual(MinerId.GetCurrentMinerIdAsync().Result, response.MinerId);
      var blockchainInfo = await BlockChainInfo.GetInfoAsync();
      Assert.AreEqual(blockchainInfo.BestBlockHeight, response.CurrentHighestBlockHeight);
      Assert.AreEqual(blockchainInfo.BestBlockHash, response.CurrentHighestBlockHash);
    }

    protected async Task Assert2ValidAnd1InvalidAsync(SubmitTransactionsResponseViewModel response)
    {

      // tx1 and tx2 should be acccepted, bzt txZeroFee should not be
      rpcClientFactoryMock.AllCalls.AssertContains("mocknode0:sendrawtransactions/", "mocknode0:sendrawtransactions/" + txC3Hash + "/" + tx2Hash);

      await ValidateHeaderSubmitTransactionsAsync(response);

      // validate individual transactions
      Assert.AreEqual(1, response.FailureCount);
      Assert.AreEqual(3, response.Txs.Length);

      // Failures are listed first
      Assert.AreEqual(txZeroFeeHash, response.Txs[0].Txid);
      Assert.AreEqual("failure", response.Txs[0].ReturnResult);
      Assert.AreEqual("Not enough fees", response.Txs[0].ResultDescription);
      await AssertTxStatus(txZeroFeeHash, TxStatus.NotPresentInDb);

      Assert.AreEqual(txC3Hash, response.Txs[1].Txid);
      Assert.AreEqual("success", response.Txs[1].ReturnResult);
      Assert.AreEqual("", response.Txs[1].ResultDescription);
      await AssertTxStatus(txC3Hash, TxStatus.Mempool);

      Assert.AreEqual(tx2Hash, response.Txs[2].Txid);
      Assert.AreEqual("success", response.Txs[2].ReturnResult);
      Assert.AreEqual("", response.Txs[2].ResultDescription);
      await AssertTxStatus(tx2Hash, TxStatus.Mempool);
    }

    protected async Task AssertQueryTxAsync(QueryTransactionStatusResponseViewModel response, string expectedTxId, string expectedResult = "success", string expectedDescription = "")
    {
      Assert.AreEqual("1.4.0", response.ApiVersion);
      Assert.IsTrue((MockedClock.UtcNow - response.Timestamp).TotalSeconds < 60);
      Assert.AreEqual(expectedTxId, response.Txid);
      Assert.AreEqual(expectedResult, response.ReturnResult);
      Assert.AreEqual(expectedDescription, response.ResultDescription);

      Assert.AreEqual(MinerId.GetCurrentMinerIdAsync().Result, response.MinerId);
      var blockChainInfo = await BlockChainInfo.GetInfoAsync();
      Assert.AreEqual(blockChainInfo.BestBlockHeight, response.BlockHeight);
      Assert.AreEqual(blockChainInfo.BestBlockHash, response.BlockHash);
      Assert.AreEqual(0, response.TxSecondMempoolExpiry);

      if (expectedResult == "success")
      {
        await AssertTxStatus(response.Txid, TxStatus.Mempool);
      }
      else
      {
        await AssertTxStatus(response.Txid, TxStatus.NotPresentInDb);
      }
    }
  }
}
