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
using MerchantAPI.APIGateway.Test.Functional.Server;
using System.Net.Mime;
using System.Net;
using System.Net.Http.Headers;

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

      Assert.AreEqual(Const.MERCHANT_API_VERSION, response.ApiVersion);
      Assert.IsTrue((MockedClock.UtcNow - response.Timestamp).TotalSeconds < 60);

      Assert.AreEqual(MinerId.GetCurrentMinerIdAsync().Result, response.MinerId);
      var blockChainInfo = await BlockChainInfo.GetInfoAsync();
      Assert.AreEqual(blockChainInfo.BestBlockHeight, response.CurrentHighestBlockHeight);
      Assert.AreEqual(blockChainInfo.BestBlockHash, response.CurrentHighestBlockHash);
      Assert.AreEqual(CallbackIPaddresses,
                response.Callbacks != null ? String.Join(",", response.Callbacks.Select(x => x.IPAddress)) : null);
    }

    protected async Task AssertIsOKAsync(
      SubmitTransactionResponseViewModel response,
      string expectedTxId,
      string expectedResult = "success",
      string expectedDescription = "",
      bool expectedRetryableFailure = false)
    {
      // typical flow for successful submit
      if (expectedResult == "success")
      {
        await AssertIsOKAsync(response, expectedTxId, TxStatus.Accepted, expectedResult, expectedDescription, false);
      }
      else
      {
        await AssertIsOKAsync(response, expectedTxId, TxStatus.NotPresentInDb, expectedResult, expectedDescription, expectedRetryableFailure);
      }
    }

    protected async Task AssertIsOKAsync(
      SubmitTransactionResponseViewModel response,
      string expectedTxId,
      int txStatus,
      string expectedResult = "success",
      string expectedDescription = "",
      bool expectedRetryableFailure = false)
    {
      Assert.AreEqual(Const.MERCHANT_API_VERSION, response.ApiVersion);
      Assert.IsTrue((MockedClock.UtcNow - response.Timestamp).TotalSeconds < 60);
      Assert.AreEqual(expectedResult, response.ReturnResult);
      // Description should be "" (not null)
      Assert.AreEqual(expectedDescription, response.ResultDescription);
      Assert.AreEqual(expectedRetryableFailure, response.FailureRetryable);

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
      Assert.AreEqual(Const.MERCHANT_API_VERSION, response.ApiVersion);
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
      await AssertTxStatus(txC3Hash, TxStatus.Accepted);

      Assert.AreEqual(tx2Hash, response.Txs[2].Txid);
      Assert.AreEqual("success", response.Txs[2].ReturnResult);
      Assert.AreEqual("", response.Txs[2].ResultDescription);
      await AssertTxStatus(tx2Hash, TxStatus.Accepted);
    }

    protected async Task<(SignedPayloadViewModel response, HttpResponseMessage httpResponse)> SubmitTxToMapiAsync(string txHex, bool dsCheck = false, bool merkleProof = false, string merkleFormat = "", string customCallbackUrl = "",
      HttpStatusCode expectedStatusCode = HttpStatusCode.OK, string expectedHttpMessage = null)
    {
      var reqContent = GetJsonRequestContent(txHex, merkleProof, dsCheck, merkleFormat, customCallbackUrl);

      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);

      var (response, message) = await Post<SignedPayloadViewModel>(MapiServer.ApiMapiSubmitTransaction, Client, reqContent, expectedStatusCode);

      await CheckHttpResponseMessageDetailAsync(message, expectedHttpMessage);

      return (response, message);
    }

    protected async Task<(SignedPayloadViewModel response, HttpResponseMessage httpResponse)> SubmitTxsToMapiAsync(HttpStatusCode expectedStatusCode)
    {
      var reqContent = new StringContent($"[ {{ \"rawtx\": \"{txC3Hex}\" }}, {{ \"rawtx\": \"{txZeroFeeHex}\" }},  {{ \"rawtx\": \"{tx2Hex}\" }}]");
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);
      return await Post<SignedPayloadViewModel>(MapiServer.ApiMapiSubmitTransactions, Client, reqContent, expectedStatusCode);
    }

    public async Task AssertSubmitTxAsync(string txHex, string txHash, string expectedResult = "success", string expectedDescription = "")
    {
      var nCallsBeforeSubmit = rpcClientFactoryMock.AllCalls.FilterCalls("mocknode0:sendrawtransactions/").Count();

      var response = await SubmitTxToMapiAsync(txHex);
      VerifySignature(response);
      var payload = response.response.ExtractPayload<SubmitTransactionResponseViewModel>();
      // Check if all fields are set
      await AssertIsOKAsync(payload, txHash, expectedResult, expectedDescription);

      if (expectedResult == "success" && expectedDescription != NodeRejectCode.ResultAlreadyKnown)
      {
        var calls = rpcClientFactoryMock.AllCalls.FilterCalls("mocknode0:sendrawtransactions/" + txHash);
        Assert.AreEqual(nCallsBeforeSubmit + 1, calls.Count());
      }
      else
      {
        Assert.AreEqual(nCallsBeforeSubmit, rpcClientFactoryMock.AllCalls.FilterCalls("mocknode0:sendrawtransactions/").Count());
      }
    }
  }
}
