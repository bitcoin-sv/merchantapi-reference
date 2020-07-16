// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.APIGateway.Domain.Models;
using MerchantAPI.APIGateway.Rest.ViewModels;
using MerchantAPI.APIGateway.Test.Functional.Mock;
using MerchantAPI.APIGateway.Test.Functional.Server;
using MerchantAPI.Common.Clock;
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
  public class MapiTests : TestBase
  {
    void AddMockNode(int nodeNumber)
    {
      var mockNode = new Node(0, "mockNode"+nodeNumber, 0, "mockuserName", "mockPassword", "This is a mock node #"+nodeNumber,
        (int)NodeStatus.Connected, null, null);

      _ = Nodes.CreateNodeAsync(mockNode).Result;
    }

    [TestInitialize]
    public void TestInitialize()
    {
      Initialize(mockedServices: true);
      AddMockNode(0);
    }

    [TestCleanup]
    public void TestCleanup()
    {
      Cleanup();
    }

    static void VerifySignature((SignedPayloadViewModel response, HttpResponseMessage httpResponse) response)
    {
      Assert.IsTrue(JsonEnvelopeSignature.VerifySignature(response.response.ToDomainObject()), "Signature is invalid");
    }

    void AssertIsOK(SubmitTransactionResponseViewModel response, string expectedTxId, string expectedResult = "success", string expectedDescription = "")
    {

      Assert.AreEqual("0.1.2", response.ApiVersion);
      Assert.IsTrue((DateTime.UtcNow - response.Timestamp).TotalSeconds < 60);
      Assert.AreEqual(expectedResult, response.ReturnResult);
      Assert.AreEqual(expectedDescription, response.ResultDescription);

      Assert.AreEqual(MinerId.GetCurrentMinerIdAsync().Result, response.MinerId);
      Assert.AreEqual(BlockChainInfo.GetInfo().BestBlockHeight, response.CurrentHighestBlockHeight);
      Assert.AreEqual(BlockChainInfo.GetInfo().BestBlockHash, response.CurrentHighestBlockHash);
      Assert.AreEqual(expectedTxId, response.Txid);
    }
    void AssertIsOK(FeeQuoteViewModelGet response)
    {

      Assert.AreEqual("0.1.2", response.ApiVersion);
      Assert.IsTrue((DateTime.UtcNow - response.CreatedAt).TotalSeconds < 60);

      Assert.AreEqual(MinerId.GetCurrentMinerIdAsync().Result, response.MinerId);
      Assert.AreEqual(BlockChainInfo.GetInfo().BestBlockHeight, response.CurrentHighestBlockHeight);
      Assert.AreEqual(BlockChainInfo.GetInfo().BestBlockHash, response.CurrentHighestBlockHash);
    }

    [TestMethod]
    public async Task GetFeeQuote()
    {
      var response = await Get<SignedPayloadViewModel>(
        MapiServer.ApiMapiQueryFeeQuote, client, HttpStatusCode.OK);

      var payload = response.response.ExtractPayload<FeeQuoteViewModelGet>();
      AssertIsOK(payload);

      using (MockedClock.NowIs(DateTime.UtcNow.AddMinutes(FeeQuoteRepositoryMock.quoteExpiryMinutes + 1)))
      {
        // should return same
        response = await Get<SignedPayloadViewModel>(
          MapiServer.ApiMapiQueryFeeQuote, client, HttpStatusCode.OK);

        payload = response.response.ExtractPayload<FeeQuoteViewModelGet>();
        AssertIsOK(payload);
      }

    }


    [TestMethod]
    public async Task GetFeeQuoteAuthenticated()
    {
      RestAuthentication = "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiI1IiwibmJmIjoxNTk5NDExNDQzLCJleHAiOjE5MTQ3NzE0NDMsImlhdCI6MTU5OTQxMTQ0MywiaXNzIjoiaHR0cDovL215c2l0ZS5jb20iLCJhdWQiOiJodHRwOi8vbXlhdWRpZW5jZS5jb20ifQ.Z43NASAbIxMZrL2MzbJTJD30hYCxhoAs-8heDjQMnjM";
      _ = await Get<SignedPayloadViewModel>(
                     MapiServer.ApiMapiQueryFeeQuote, client, HttpStatusCode.NotFound); 

      feeQuoteRepositoryMock.FeeFileName = "feeQuotesWithIdentity.json";
      (SignedPayloadViewModel response, HttpResponseMessage httpResponse) response = await Get<SignedPayloadViewModel>(
                 MapiServer.ApiMapiQueryFeeQuote, client, HttpStatusCode.OK);
      var payload = response.response.ExtractPayload<FeeQuoteViewModelGet>();
      AssertIsOK(payload);

    }


    [TestMethod]
    public async Task SubmitTransactionBinary()
    {
      var txBytes = HelperTools.HexStringToByteArray(txC3Hex);

      var reqContent = new ByteArrayContent(txBytes);
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Octet);

      var response =
        await Post<SignedPayloadViewModel>(MapiServer.ApiMapiSubmitTransaction, client, reqContent, HttpStatusCode.OK);
      VerifySignature(response);

      rpcClientFactoryMock.AllCalls.AssertContains("mocknode0:sendrawtransactions/", "mocknode0:sendrawtransactions/" + txC3Hash);

      var payload = response.response.ExtractPayload<SubmitTransactionResponseViewModel>();

      // Check if all fields are set
      AssertIsOK(payload, txC3Hash);
    }

        [TestMethod]
    public async Task SubmitTransactionDuplicateError()
    {
      var txBytes = HelperTools.HexStringToByteArray(txC3Hex);

      var reqContent = new ByteArrayContent(txBytes);
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Octet);

      var response =
        await Post<SignedPayloadViewModel>(MapiServer.ApiMapiSubmitTransaction, client, reqContent, HttpStatusCode.OK);
      VerifySignature(response);

      rpcClientFactoryMock.AllCalls.AssertContains("mocknode0:sendrawtransactions/", "mocknode0:sendrawtransactions/" + txC3Hash);

      var payload = response.response.ExtractPayload<SubmitTransactionResponseViewModel>();

      // Check if all fields are set
      AssertIsOK(payload, txC3Hash);

      response = await Post<SignedPayloadViewModel>(MapiServer.ApiMapiSubmitTransaction, client, reqContent, HttpStatusCode.OK);
      VerifySignature(response);
      payload = response.response.ExtractPayload<SubmitTransactionResponseViewModel>();
      Assert.AreEqual(payload.ReturnResult, "failure");
      Assert.AreEqual(payload.ResultDescription, "Transaction already known");
    }

    [TestMethod]
    public async Task SubmitTransactionJson()
    {
      var reqContent = new StringContent($"{{ \"rawtx\": \"{txC3Hex}\" }}");
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);

      var response = await Post<SignedPayloadViewModel>(MapiServer.ApiMapiSubmitTransaction, client, reqContent, HttpStatusCode.OK);
      VerifySignature(response);

      rpcClientFactoryMock.AllCalls.AssertContains("mocknode0:sendrawtransactions/", "mocknode0:sendrawtransactions/" + txC3Hash);

      var payload = response.response.ExtractPayload<SubmitTransactionResponseViewModel>();

      // Check if all fields are set
      AssertIsOK(payload, txC3Hash);
      Assert.AreEqual("", payload.ResultDescription); // Description should be "" (not null)
    }

    private int GetBytesForScriptLength(ulong totalBytes)
    {
      if (totalBytes < byte.MaxValue) // uint8 == byte
      {
        return 1;
      }
      else if (totalBytes < UInt16.MaxValue) // if script length can not be encoded in single byte we need additional data
      {
        return 3; // saved as variable length integer (0xFD followed by the length as uint16_t)
      }
      else if (totalBytes < UInt32.MaxValue)
      {
        return 5;
      }
      else if (totalBytes < UInt64.MaxValue)
      {
        return 9;
      }
      else
      {
        throw new ArgumentException("Script is too big.");
      }
    }


    /// <summary>
    /// Create a new transaction with is totalBytes long. Out of this totalBytes, dataBytes are spend for 
    /// </summary>
    /// <param name="fundingTx">Input transaction. It's first output will be used as funding for new transaction</param>
    /// <param name="totalBytes">Total desired length of created transaction</param>
    /// <param name="dataBytes">Number of data bytes (OP_FALSE transaction ....) that this transaction  should contain</param>
    /// <param name="totalFees"> total fees payed by this transaction</param>
    /// <returns></returns>
    Transaction CreateTransaction(Transaction fundingTx, long totalBytes, long dataBytes, long totalFees)
    {
      if (dataBytes > 0)
      {
        if (dataBytes < 2)
        {
          throw new ArgumentException($"nameof(dataBytes) should be at least 2, since script must start with OP_FALSE OP_RETURN");
        }
      }
      
      var remainingMoney = fundingTx.Outputs[0].Value - totalFees;
      if (remainingMoney < 0L)
      {
        throw new ArgumentException("Fee is too large (or funding output is to low)");
      }

      var tx = BCash.Instance.Regtest.CreateTransaction();
      tx.Inputs.Add(new TxIn(new OutPoint(fundingTx, 0)));

      long sizeOfSingleOutputWithoutScript = sizeof(ulong) + GetBytesForScriptLength((ulong) (totalBytes - dataBytes)); // 9+:	A list of 1 or more transaction outputs or destinations for coins
      long overHead =
           tx.ToBytes().Length // length of single input
          + dataBytes == 0 ? 0 : tx.ToBytes().Length + sizeOfSingleOutputWithoutScript;

      long normalBytes = totalBytes - dataBytes - overHead;

      if (normalBytes > 0 && dataBytes > 0) // Overhead also depends on number of outputs - if this is true we have two outputs 
      {
        normalBytes -= (sizeof(ulong) + GetBytesForScriptLength((ulong)dataBytes));
      }

      if (normalBytes > 0)
      {
        var scriptBytes = new byte[normalBytes];
        tx.Outputs.Add(new TxOut(remainingMoney, new Script(scriptBytes)));
        remainingMoney = 0L;
      }
      else if (normalBytes < 0)
      {
        throw new ArgumentException("Argument Databytes is too low.");
      }
      if (dataBytes > 0)
      {
        var scriptBytes = new byte[dataBytes];
        scriptBytes[0] = (byte)OpcodeType.OP_FALSE;
        scriptBytes[1] = (byte)OpcodeType.OP_RETURN;
        tx.Outputs.Add(new TxOut(remainingMoney, new Script(scriptBytes)));
      }

      Assert.AreEqual(totalBytes, tx.ToBytes().Length, "Failed to create transaction of desired length");

      return tx;
      
    }

    [TestMethod]
    public async Task SubmitTransactionJsonMinFee()
    {
      // register tx0
      rpcClientFactoryMock.SetUpTransaction(txC0Hex);
      var tx0 = HelperTools.ParseBytesToTransaction(HelperTools.HexStringToByteArray(txC0Hex));

      int txLength = 160;
      var fee = feeQuoteRepositoryMock.GetValidFeeQuotesByIdentity(null).Single().Fees.Single(x => x.FeeType == "standard");
      var minRequiredFees = Math.Min((txLength * fee.RelayFee.Satoshis) / fee.RelayFee.Bytes, // 40
                          (txLength * fee.MiningFee.Satoshis) / fee.MiningFee.Bytes); // 80

      var tx1 = CreateTransaction(tx0, txLength, 0, minRequiredFees); // submit tx1 should succeed

      var reqContent = new StringContent($"{{ \"rawtx\": \"{ tx1.ToHex() }\" }}");
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);

      var response = await Post<SignedPayloadViewModel>(MapiServer.ApiMapiSubmitTransaction, client, reqContent, HttpStatusCode.OK);
      VerifySignature(response);

      rpcClientFactoryMock.AllCalls.AssertContains("mocknode0:sendrawtransactions/", "mocknode0:sendrawtransactions/" + tx1.GetHash().ToString());

      var payload = response.response.ExtractPayload<SubmitTransactionResponseViewModel>();

      // Check if all fields are set
      AssertIsOK(payload, tx1.GetHash().ToString());

    }

    [TestMethod]
    public async Task SubmitTransactionJsonMinSumFee()
    {
      // register tx0
      rpcClientFactoryMock.SetUpTransaction(txC0Hex);
      var tx0 = HelperTools.ParseBytesToTransaction(HelperTools.HexStringToByteArray(txC0Hex));
 
      int txLength = 160;
      var fee = feeQuoteRepositoryMock.GetValidFeeQuotesByIdentity(null).Single().Fees.Single(x => x.FeeType == "standard");
      var minRequiredFees = Math.Min((txLength * fee.RelayFee.Satoshis) / fee.RelayFee.Bytes, // 40
                          (txLength * fee.MiningFee.Satoshis) / fee.MiningFee.Bytes); // 80
      var tx1 = CreateTransaction(tx0, txLength, 0, minRequiredFees - 1); // submit tx1 should fail

      var reqContent = new StringContent($"{{ \"rawtx\": \"{ tx1.ToHex() }\" }}");
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);

      var response = await Post<SignedPayloadViewModel>(MapiServer.ApiMapiSubmitTransaction, client, reqContent, HttpStatusCode.OK);
      VerifySignature(response);

      Assert.AreEqual(0, rpcClientFactoryMock.AllCalls.FilterCalls("mocknode0:sendrawtransactions/").Count()); // no calls, to submit txs since we do not pay enough fee

      var payload = response.response.ExtractPayload<SubmitTransactionResponseViewModel>();
      // Check if all fields are set
      AssertIsOK(payload, tx1.GetHash().ToString(), "failure", "Not enough fees");

      var tx2 = CreateTransaction(tx0, txLength, 0, minRequiredFees + 1); // submit tx2 should succeed

      reqContent = new StringContent($"{{ \"rawtx\": \"{ tx2.ToHex() }\" }}");
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);

      response = await Post<SignedPayloadViewModel>(MapiServer.ApiMapiSubmitTransaction, client, reqContent, HttpStatusCode.OK);
      VerifySignature(response);

      rpcClientFactoryMock.AllCalls.AssertContains("mocknode0:sendrawtransactions/", "mocknode0:sendrawtransactions/" + tx2.GetHash());

      payload = response.response.ExtractPayload<SubmitTransactionResponseViewModel>();

      // Check if all fields are set
      AssertIsOK(payload, tx2.GetHash().ToString());
    }


    [TestMethod]
    public async Task SubmitTransactionJsonMinFeeTypeData()
    {
      // register tx0
      rpcClientFactoryMock.SetUpTransaction(txC0Hex);
      var tx0 = HelperTools.ParseBytesToTransaction(HelperTools.HexStringToByteArray(txC0Hex));

      long txLength = 160;
      long dataLength = 100;
      long standard = txLength-dataLength;
      var fee = feeQuoteRepositoryMock.GetValidFeeQuotesByIdentity(null).Single().Fees.Single(x => x.FeeType == "data");
      var minRequiredFees = Math.Min((dataLength * fee.RelayFee.Satoshis) / fee.RelayFee.Bytes, // 20
                    (dataLength * fee.MiningFee.Satoshis) / fee.MiningFee.Bytes); // 40
      fee = feeQuoteRepositoryMock.GetValidFeeQuotesByIdentity(null).Single().Fees.Single(x => x.FeeType == "standard");
      minRequiredFees +=  Math.Min((standard * fee.RelayFee.Satoshis) / fee.RelayFee.Bytes, // 15
              (standard * fee.MiningFee.Satoshis) / fee.MiningFee.Bytes); // 30


      var tx1 = CreateTransaction(tx0, txLength, dataLength, minRequiredFees); // submit tx1 should succeed

      var reqContent = new StringContent($"{{ \"rawtx\": \"{ tx1.ToHex() }\" }}");
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);

      var response = await Post<SignedPayloadViewModel>(MapiServer.ApiMapiSubmitTransaction, client, reqContent, HttpStatusCode.OK);
      VerifySignature(response);

      rpcClientFactoryMock.AllCalls.AssertContains("mocknode0:sendrawtransactions/", "mocknode0:sendrawtransactions/" + tx1.GetHash().ToString());

      var payload = response.response.ExtractPayload<SubmitTransactionResponseViewModel>();

      // Check if all fields are set
      AssertIsOK(payload, tx1.GetHash().ToString());

    }

    public async Task SubmitTransactionJsonMinSumFeeTypeData()
    {
      // register tx0
      rpcClientFactoryMock.SetUpTransaction(txC0Hex);
      var tx0 = HelperTools.ParseBytesToTransaction(HelperTools.HexStringToByteArray(txC0Hex));

      long txLength = 160;
      long dataLength = 100;
      long standard = txLength - dataLength;
      var fee = feeQuoteRepositoryMock.GetValidFeeQuotesByIdentity(null).Single().Fees.Single(x => x.FeeType == "data");
      var minRequiredFees = Math.Min((dataLength * fee.RelayFee.Satoshis) / fee.RelayFee.Bytes, // 20
                    (dataLength * fee.MiningFee.Satoshis) / fee.MiningFee.Bytes); // 40
      fee = feeQuoteRepositoryMock.GetValidFeeQuotesByIdentity(null).Single().Fees.Single(x => x.FeeType == "standard");
      minRequiredFees += Math.Min((standard * fee.RelayFee.Satoshis) / fee.RelayFee.Bytes, // 15
              (standard * fee.MiningFee.Satoshis) / fee.MiningFee.Bytes); // 30

      var tx1 = CreateTransaction(tx0, txLength, 0, minRequiredFees - 1); // submit tx1 should fail

      var reqContent = new StringContent($"{{ \"rawtx\": \"{ tx1.ToHex() }\" }}");
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);

      var response = await Post<SignedPayloadViewModel>(MapiServer.ApiMapiSubmitTransaction, client, reqContent, HttpStatusCode.OK);
      VerifySignature(response);

      Assert.AreEqual(0, rpcClientFactoryMock.AllCalls.FilterCalls("mocknode0:sendrawtransactions/").Count()); // no calls, to submit txs since we do not pay enough fee

      var payload = response.response.ExtractPayload<SubmitTransactionResponseViewModel>();
      // Check if all fields are set
      AssertIsOK(payload, tx1.GetHash().ToString(), "failure", "Not enough fees");


      var tx2 = CreateTransaction(tx0, txLength, 0, minRequiredFees + 1); // submit tx2 should succeed

      reqContent = new StringContent($"{{ \"rawtx\": \"{ tx2.ToHex() }\" }}");
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);

      response = await Post<SignedPayloadViewModel>(MapiServer.ApiMapiSubmitTransaction, client, reqContent, HttpStatusCode.OK);
      VerifySignature(response);

      rpcClientFactoryMock.AllCalls.AssertContains("mocknode0:sendrawtransactions/", "mocknode0:sendrawtransactions/" + tx2.GetHash().ToString());

      payload = response.response.ExtractPayload<SubmitTransactionResponseViewModel>();

      // Check if all fields are set
      AssertIsOK(payload, tx2.GetHash().ToString());

    }

    [TestMethod]
    public async Task SubmitTransactionJsonMinFeeTypeDataTestBigLength()
    {
      // register tx0
      rpcClientFactoryMock.SetUpTransaction(txC0Hex);
      var tx0 = HelperTools.ParseBytesToTransaction(HelperTools.HexStringToByteArray(txC0Hex));

      long txLength = 400000;
      long dataLength = 350000;
      long standard = txLength - dataLength;
      var fee = feeQuoteRepositoryMock.GetValidFeeQuotesByIdentity(null).Single().Fees.Single(x => x.FeeType == "data");
      var minRequiredFees = Math.Min((dataLength * fee.RelayFee.Satoshis) / fee.RelayFee.Bytes, 
                    (dataLength * fee.MiningFee.Satoshis) / fee.MiningFee.Bytes); 
      fee = feeQuoteRepositoryMock.GetValidFeeQuotesByIdentity(null).Single().Fees.Single(x => x.FeeType == "standard");
      minRequiredFees += Math.Min((standard * fee.RelayFee.Satoshis) / fee.RelayFee.Bytes, 
              (standard * fee.MiningFee.Satoshis) / fee.MiningFee.Bytes); 


      var tx1 = CreateTransaction(tx0, txLength, dataLength, minRequiredFees); // submit tx1 should succeed

      var reqContent = new StringContent($"{{ \"rawtx\": \"{ tx1.ToHex() }\" }}");
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);

      var response = await Post<SignedPayloadViewModel>(MapiServer.ApiMapiSubmitTransaction, client, reqContent, HttpStatusCode.OK);
      VerifySignature(response);

      rpcClientFactoryMock.AllCalls.AssertContains("mocknode0:sendrawtransactions/", "mocknode0:sendrawtransactions/" + tx1.GetHash().ToString());

      var payload = response.response.ExtractPayload<SubmitTransactionResponseViewModel>();

      // Check if all fields are set
      AssertIsOK(payload, tx1.GetHash().ToString());

    }


    [TestMethod]
    [DataRow("something", HttpStatusCode.BadRequest)]
    [DataRow("invalidScheme://www.something.com", HttpStatusCode.BadRequest)]
    [DataRow("http://www.something.com", HttpStatusCode.OK)]
    [DataRow("https://www.something.com", HttpStatusCode.OK)]
    public async Task SubmitTransactionJsonInvalidCallBackUrl(string url, HttpStatusCode expectedCode)
    {

      // Test submitting single tx through JSON
      var reqContent = new StringContent($"{{ \"rawtx\": \"{txC0Hex}\", \"callbackUrl\" : \"{url}\" }}");
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);

      _ = await Post<SignedPayloadViewModel>(MapiServer.ApiMapiSubmitTransaction, client, reqContent, expectedCode);

      // Test submitting multiple txs through JSON, include callback in URL
      reqContent = new StringContent($"[ {{ \"rawtx\": \"{txC1Hex}\", \"callbackUrl\" : \"{url}\" }}]");
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);
      _ = await Post<SignedPayloadViewModel>(MapiServer.ApiMapiSubmitTransactions, client, reqContent, expectedCode);

      // Test submitting multiple txs through JSON, use default callback
      reqContent = new StringContent($"[ {{ \"rawtx\": \"{txC2Hex}\" }}]");
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);
      _ = await Post<SignedPayloadViewModel>(MapiServer.ApiMapiSubmitTransactions+ $"?defaultCallbackUrl={url}", client, reqContent, expectedCode);


      // Test submitting multiple txs through JSON
      reqContent = new StringContent($"[ {{ \"rawtx\": \"{txC3Hex}\", \"callbackUrl\" : \"{url}\" }}]");
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);
      _ = await Post<SignedPayloadViewModel>(MapiServer.ApiMapiSubmitTransactions, client, reqContent, expectedCode);


      // Test submitting single transaction through Binary
      var txBytes = HelperTools.HexStringToByteArray(txZeroFeeHex);

      var reqContentBin = new ByteArrayContent(txBytes);
      reqContentBin.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Octet);

      _ = await Post<SignedPayloadViewModel>(MapiServer.ApiMapiSubmitTransaction+$"?callbackUrl={url}", client, reqContentBin, expectedCode);

      // Test submitting multiple  transaction through Binary
      txBytes = HelperTools.HexStringToByteArray(tx2Input1Hex);

      reqContentBin = new ByteArrayContent(txBytes);
      reqContentBin.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Octet);

      _ = await Post<SignedPayloadViewModel>(MapiServer.ApiMapiSubmitTransactions + $"?callbackUrl={url}", client, reqContentBin, expectedCode);

    }


    [TestMethod]
    public async Task SubmitTransactionJsonAuthenticated()
    {
      // use special free fee policy for user
      feeQuoteRepositoryMock.FeeFileName = "feeQuotesWithIdentity.json";

      var reqContent = new StringContent($"{{ \"rawtx\": \"{txZeroFeeHex}\" }}");
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);

      // txZeroFeeHex - it should fail without authentication
      var response = await Post<SignedPayloadViewModel>(MapiServer.ApiMapiSubmitTransaction, client, reqContent, HttpStatusCode.OK);
      VerifySignature(response);
      Assert.AreEqual(0, rpcClientFactoryMock.AllCalls.FilterCalls("mocknode0:sendrawtransactions/").Count()); // no calls, to submit txs since we do not pay enough fee

      var payload = response.response.ExtractPayload<SubmitTransactionResponseViewModel>();
      // Check if all fields are set
      AssertIsOK(payload, txZeroFeeHash, "failure", "Not enough fees");

      // Test token valid until year 2030. Generate with:
      //    TokenManager.exe generate -n 5 -i http://mysite.com -a http://myaudience.com -k thisisadevelopmentkey -d 3650
      //
      RestAuthentication = "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiI1IiwibmJmIjoxNTk5NDExNDQzLCJleHAiOjE5MTQ3NzE0NDMsImlhdCI6MTU5OTQxMTQ0MywiaXNzIjoiaHR0cDovL215c2l0ZS5jb20iLCJhdWQiOiJodHRwOi8vbXlhdWRpZW5jZS5jb20ifQ.Z43NASAbIxMZrL2MzbJTJD30hYCxhoAs-8heDjQMnjM";
      // now it should succeed for this user
      response = await Post<SignedPayloadViewModel>(MapiServer.ApiMapiSubmitTransaction, client, reqContent, HttpStatusCode.OK);
      VerifySignature(response);

      rpcClientFactoryMock.AllCalls.AssertContains("mocknode0:sendrawtransactions/", "mocknode0:sendrawtransactions/" + txZeroFeeHash);
      payload = response.response.ExtractPayload<SubmitTransactionResponseViewModel>();

      // Check if all fields are set
      AssertIsOK(payload, txZeroFeeHash);
    }



    [TestMethod]
    public async Task SubmitTransactionJsonFeeQuoteExpired()
    {
      // use special free fee policy 
      feeQuoteRepositoryMock.FeeFileName = "feeQuotesAllFree.json";
      var feeQuotes = feeQuoteRepositoryMock.GetValidFeeQuotesByIdentity(null);
      Assert.AreEqual(1, feeQuotes.Count()); 

      var reqContent = new StringContent($"{{ \"rawtx\": \"{txZeroFeeHex}\" }}");
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);

      using (MockedClock.NowIs(DateTime.UtcNow.AddMinutes(FeeQuoteRepositoryMock.quoteExpiryMinutes)))
      {
        feeQuotes = feeQuoteRepositoryMock.GetValidFeeQuotesByIdentity(null);
        Assert.AreEqual(1, feeQuotes.Count()); // should return current

        var response = await Post<SignedPayloadViewModel>(MapiServer.ApiMapiSubmitTransaction, client, reqContent, HttpStatusCode.OK);
        VerifySignature(response);

        rpcClientFactoryMock.AllCalls.AssertContains("mocknode0:sendrawtransactions/", "mocknode0:sendrawtransactions/" + txZeroFeeHash);
        var payload = response.response.ExtractPayload<SubmitTransactionResponseViewModel>();

        // Check if all fields are set
        AssertIsOK(payload, txZeroFeeHash);
      }

    }

    [TestMethod]
    public async Task SubmitTransactionJsonTwoValidFeeQuotes()
    {
      // use special free fee policy 
      feeQuoteRepositoryMock.FeeFileName = "feeQuotesWithCreatedAt.json";

      var feeQuotes = feeQuoteRepositoryMock.GetValidFeeQuotesByIdentity(null);
      Assert.AreEqual(1, feeQuotes.Count()); // current feeQuote is valid now

      var reqContent = new StringContent($"{{ \"rawtx\": \"{txZeroFeeHex}\" }}");
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);

      using (MockedClock.NowIs(new DateTime(2020, 9, 1, 12, 6, 0))) // go back in time
      {
        feeQuotes = feeQuoteRepositoryMock.GetValidFeeQuotesByIdentity(null);
        Assert.AreEqual(2, feeQuotes.Count());

        var response = await Post<SignedPayloadViewModel>(MapiServer.ApiMapiSubmitTransaction, client, reqContent, HttpStatusCode.OK);
        VerifySignature(response);

        rpcClientFactoryMock.AllCalls.AssertContains("mocknode0:sendrawtransactions/", "mocknode0:sendrawtransactions/" + txZeroFeeHash);
        var payload = response.response.ExtractPayload<SubmitTransactionResponseViewModel>();

        // Check if all fields are set
        AssertIsOK(payload, txZeroFeeHash);
      }

    }

    [TestMethod]
    public async Task SubmitTransactionJsonTwoNodes()
    {
      AddMockNode(1);
      Assert.AreEqual(2, Nodes.GetNodes().Count());


      var reqContent = new StringContent($"{{ \"rawtx\": \"{txC3Hex}\" }}");
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);

      var response = await Post<SignedPayloadViewModel>(MapiServer.ApiMapiSubmitTransaction, client, reqContent, HttpStatusCode.OK);
      VerifySignature(response);

      rpcClientFactoryMock.AllCalls.AssertContains("mocknode0:sendrawtransactions/", "mocknode0:sendrawtransactions/" + txC3Hash);
      rpcClientFactoryMock.AllCalls.AssertContains("mocknode1:sendrawtransactions/", "mocknode1:sendrawtransactions/" + txC3Hash);
      
      var payload = response.response.ExtractPayload<SubmitTransactionResponseViewModel>();

      // Check if all fields are set
      AssertIsOK(payload, txC3Hash);
    }

    [TestMethod]
    public async Task SubmitTransactionJsonTwoNodesOneDown()
    {
      AddMockNode(1);
      Assert.AreEqual(2, Nodes.GetNodes().Count());

      rpcClientFactoryMock.DisconnectNode("mocknode0");

      var reqContent = new StringContent($"{{ \"rawtx\": \"{txC3Hex}\" }}");
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);

      var response = await Post<SignedPayloadViewModel>(MapiServer.ApiMapiSubmitTransaction, client, reqContent, HttpStatusCode.OK);
      VerifySignature(response);

      rpcClientFactoryMock.AllCalls.AssertContains("mocknode1:sendrawtransactions/", "mocknode1:sendrawtransactions/" + txC3Hash);

      var payload = response.response.ExtractPayload<SubmitTransactionResponseViewModel>();

      // Check if all fields are set
      AssertIsOK(payload, txC3Hash);
    }


    [TestMethod]
    public async Task SubmitTransactionJsonBothNodesDown_should_return_failure()
    {
      AddMockNode(1);
      Assert.AreEqual(2, Nodes.GetNodes().Count());

      rpcClientFactoryMock.DisconnectNode("mocknode0");
      

      var reqContent = new StringContent($"{{ \"rawtx\": \"{txC3Hex}\" }}");
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);


      // fetch blockchain info while one node is sitll available and then last connected node
      _ = blockChainInfo.GetInfo();
      rpcClientFactoryMock.DisconnectNode("mocknode1");

      var response = await Post<SignedPayloadViewModel>(MapiServer.ApiMapiSubmitTransaction, client, reqContent, HttpStatusCode.OK);
      VerifySignature(response);

      var payload = response.response.ExtractPayload<SubmitTransactionResponseViewModel>();

      Assert.AreEqual("failure", payload.ReturnResult);
    }


    [TestMethod]
    public async Task SubmitTransactionZeroFeeJson()
    {
      var reqContent = new StringContent($"{{ \"rawtx\": \"{txZeroFeeHex}\" }}");
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);

      var response = await Post<SignedPayloadViewModel>(MapiServer.ApiMapiSubmitTransaction, client, reqContent, HttpStatusCode.OK);
      VerifySignature(response);

      Assert.AreEqual(0, rpcClientFactoryMock.AllCalls.FilterCalls("mocknode0:sendrawtransactions/").Count()); // no calls, to submit txs since we do not pay enough fee
      
      // We still expect one call to fetch previous outputs (we need to check them to calculate fee) 
      Assert.AreEqual(1, rpcClientFactoryMock.AllCalls.FilterCalls("mocknode0:gettxouts").Count());

      var payload = response.response.ExtractPayload<SubmitTransactionResponseViewModel>();

      // Check if all fields are set
      AssertIsOK(payload, txZeroFeeHash,"failure", "Not enough fees");
    }



    void Assert2ValidAnd1Invalid(SubmitTransactionsResponseViewModel response)
    {

      // tx1 and tx2 should be acccepted, bzt txZeroFee should not be
      rpcClientFactoryMock.AllCalls.AssertContains("mocknode0:sendrawtransactions/", "mocknode0:sendrawtransactions/" + txC3Hash + "/" + tx2Hash);

      // validate header
      Assert.AreEqual("0.1.2", response.ApiVersion);
      Assert.IsTrue((DateTime.UtcNow - response.Timestamp).TotalSeconds < 60);
      Assert.AreEqual(MinerId.GetCurrentMinerIdAsync().Result, response.MinerId);
      Assert.AreEqual(BlockChainInfo.GetInfo().BestBlockHeight, response.CurrentHighestBlockHeight);
      Assert.AreEqual(BlockChainInfo.GetInfo().BestBlockHash, response.CurrentHighestBlockHash);

      // validate individual transactions
      Assert.AreEqual(1, response.FailureCount);
      Assert.AreEqual(3, response.Txs.Length);

      // Failures are listed first
      Assert.AreEqual(txZeroFeeHash, response.Txs[0].Txid);
      Assert.AreEqual("failure", response.Txs[0].ReturnResult);
      Assert.AreEqual("Not enough fees", response.Txs[0].ResultDescription);

      Assert.AreEqual(txC3Hash, response.Txs[1].Txid);
      Assert.AreEqual("success", response.Txs[1].ReturnResult);
      Assert.AreEqual(null, response.Txs[1].ResultDescription);

      Assert.AreEqual(tx2Hash, response.Txs[2].Txid);
      Assert.AreEqual("success", response.Txs[2].ReturnResult);
      Assert.AreEqual(null, response.Txs[2].ResultDescription);
    }

    [TestMethod]
    public async Task SubmitTransactionsJson()
    {
      var reqContent = new StringContent($"[ {{ \"rawtx\": \"{txC3Hex}\" }}, {{ \"rawtx\": \"{txZeroFeeHex}\" }},  {{ \"rawtx\": \"{tx2Hex}\" }}]");
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);
      var response = await Post<SignedPayloadViewModel>(MapiServer.ApiMapiSubmitTransactions, client, reqContent, HttpStatusCode.OK);
      VerifySignature(response);


      Assert2ValidAnd1Invalid(response.response.ExtractPayload<SubmitTransactionsResponseViewModel>());
    }

    [TestMethod]
    public async Task SubmitTransactionsJsonChain()
    {
      // use free fee policy, since the transactions we use are not paying any fee
      feeQuoteRepositoryMock.FeeFileName = "feeQuotesAllFree.json";
      // just register tx0, the rests should get inputs from the bbatc
      rpcClientFactoryMock.SetUpTransaction(txC0Hex);

      var reqContent = new StringContent($"[ {{ \"rawtx\": \"{txC1Hex}\" }}, {{ \"rawtx\": \"{txC2Hex}\" }},  {{ \"rawtx\": \"{txC3Hex}\" }}]");
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);
      var response = await Post<SignedPayloadViewModel>(MapiServer.ApiMapiSubmitTransactions, client, reqContent, HttpStatusCode.OK);
      VerifySignature(response);

      var p = response.response.ExtractPayload<SubmitTransactionsResponseViewModel>();
      Assert.AreEqual(0, p.FailureCount);
      Assert.AreEqual(3,p.Txs.Length);

      Assert.AreEqual(1, rpcClientFactoryMock.AllCalls.FilterCalls("mocknode0:gettxouts").Count(), "just 1 call toward the node was expected");

    }

    [TestMethod]
    public async Task SubmitTransactionsBinary()
    {
      var bytes = HelperTools.HexStringToByteArray(txC3Hex + txZeroFeeHex + tx2Hex); 
      
      var reqContent = new ByteArrayContent(bytes);
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Octet);
      var response = await Post<SignedPayloadViewModel>(MapiServer.ApiMapiSubmitTransactions, client, reqContent, HttpStatusCode.OK);
      VerifySignature(response);

      Assert2ValidAnd1Invalid(response.response.ExtractPayload<SubmitTransactionsResponseViewModel>());
    }


  }
}
