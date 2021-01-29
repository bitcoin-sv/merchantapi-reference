// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.Common.Clock;
using MerchantAPI.Common.Json;
using MerchantAPI.PaymentAggregator.Domain.ViewModels;
using MerchantAPI.PaymentAggregator.Infrastructure.Repositories;
using MerchantAPI.PaymentAggregator.Rest.ViewModels;
using MerchantAPI.PaymentAggregator.Test.Functional.Mock;
using MerchantAPI.PaymentAggregator.Test.Functional.Server;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Threading.Tasks;

namespace MerchantAPI.PaymentAggregator.Test.Functional
{
  [TestClass]
  public class AggregatorTests: AggregatorTestBase
  {
    [TestInitialize]
    override public void TestInitialize()
    {
      base.TestInitialize();
    }

    [TestCleanup]
    override public void TestCleanup()
    {
      base.TestCleanup();
    }

    void AssertIsOK(AllFeeQuotesViewModelGet response)
    {
      Assert.IsNotNull(response.Miner);
      foreach(var miner in response.Miner)
      {
        Assert.IsNotNull(miner.SLA);
        Assert.IsNotNull(miner.Payload);
        var payload = miner.ExtractPayload<FeeQuoteViewModelGet>();
        Assert.IsTrue((MockedClock.UtcNow - payload.Timestamp).TotalSeconds < 60);
      }
    }

    void CheckSlaLevel(MinerFeeQuoteViewModelGet response, int level)
    {
      var serviceLevels = ServiceLevelRepository.GetServiceLevels();
      var serviceLevel = serviceLevels.Single(x => x.Level == level);
      foreach (var sla in response.SLA)
      {
        Assert.AreEqual(sla.SlaCategory, serviceLevel.Level);
        Assert.AreEqual(sla.SlaDescription, serviceLevel.Description);
        foreach(var feeType in Consts.Const.FeeType.RequiredFeeTypes)
        {
          if (level > 0)
          {
            var lowerLevel = serviceLevels.Single(x => x.Level == level - 1);
            var lowerFee = lowerLevel.Fees.Single(x => x.FeeType == feeType);
            var payload = response.ExtractPayload<FeeQuoteViewModelGet>();
            foreach (var fee in payload.Fees.Where(x => x.FeeType == feeType))
            {
              Assert.IsTrue(
                (lowerFee.MiningFee.GetSatoshiPerByte() < fee.MiningFee.ToDomainObject(Consts.Const.AmountType.MiningFee).GetSatoshiPerByte())
                ||
                (lowerFee.RelayFee.GetSatoshiPerByte() < fee.RelayFee.ToDomainObject(Consts.Const.AmountType.RelayFee).GetSatoshiPerByte())
                );
            }
          }
          if (level < serviceLevels.Max(x => x.Level))
          {
            var higherLevel = serviceLevels.Single(x => x.Level == level + 1);
            if (higherLevel.Fees != null) // highest level has no fees (limit)
            {
              var higherFee = higherLevel.Fees.Single(x => x.FeeType == feeType);
              var payload = response.ExtractPayload<FeeQuoteViewModelGet>();
              foreach (var fee in payload.Fees.Where(x => x.FeeType == feeType))
              {
                Assert.IsTrue(
                  (higherFee.MiningFee.GetSatoshiPerByte() > fee.MiningFee.ToDomainObject(Consts.Const.AmountType.MiningFee).GetSatoshiPerByte())
                  ||
                  (higherFee.RelayFee.GetSatoshiPerByte() > fee.RelayFee.ToDomainObject(Consts.Const.AmountType.RelayFee).GetSatoshiPerByte())
                  );
              }
            }
          }
        }
      }
    }

    [TestMethod]
    public async Task GetAllFeeQuotes()
    {
      GatewayRepository.DeleteGateway(1); // no gateways

      CreateGateway("feeQuotesNonePublic.json"); // only one gateway, no public feeQuotes 
      (_, HttpResponseMessage httpResponse) = await GetWithHttpResponseReturned<AllFeeQuotesViewModelGet>(
        client, MapiServer.ApiAggregatorAllFeeQuotesUrl, HttpStatusCode.NotFound);
      Assert.AreEqual("Not Found", httpResponse.ReasonPhrase);

      CreateGateway("feeQuotesWithIdentity.json");
      var response = await Get<AllFeeQuotesViewModelGet>(
        client, MapiServer.ApiAggregatorAllFeeQuotesUrl, HttpStatusCode.OK);
      AssertIsOK(response);
    }

    [TestMethod]
    public async Task GetAllFeeQuotes_TestUnreachableGateway()
    {
      CreateGateway(reachable: false);
      // call must succeed, since we have one reachable and one unreachable gateway
      var response = await Get<AllFeeQuotesViewModelGet>(
        client, MapiServer.ApiAggregatorAllFeeQuotesUrl, HttpStatusCode.OK);
      AssertIsOK(response);

      // remove reachable gateway
      GatewayRepository.DeleteGateway(1); 
      // call must fail
      response = await Get<AllFeeQuotesViewModelGet>(
        client, MapiServer.ApiAggregatorAllFeeQuotesUrl, HttpStatusCode.ServiceUnavailable);
      Assert.IsNull(response);
    }

    [TestMethod]
    public async Task GetAllFeeQuotes_WithoutAccount()
    {
      AccountRepositoryPostgres.EmptyRepository(DbConnectionString);
      var response = await Get<AllFeeQuotesViewModelGet>(
        client, MapiServer.ApiAggregatorAllFeeQuotesUrl, HttpStatusCode.Unauthorized);
      Assert.IsNull(response);
    }

    [TestMethod]
    public async Task GetAllFeeQuotes_WithoutSubscription()
    {
      var response = await GetAllFeeQuotesWithoutSubscriptionAsync();
      Assert.IsNull(response);
    }

    [TestMethod]
    public async Task GetAllFeeQuotes_WithInvalidAuthentication()
    {
      var response = await GetAllFeeQuotesWithInvalidAuthenticationAsync();
      Assert.IsNull(response);
      RestAuthentication = null;

      ApiKeyAuthentication = AppSettings.RestAdminAPIKey;
      response = await Get<AllFeeQuotesViewModelGet>(
        client, MapiServer.ApiAggregatorAllFeeQuotesUrl, HttpStatusCode.Unauthorized);
      Assert.IsNull(response);
    }

    [TestMethod]
    public async Task GetAllFeeQuotes_WithSLA0()
    {
      GatewayRepository.DeleteGateway(1); // no gateways 

      // all fees are zero and match sla0
      CreateGateway("feeQuotesSLA0.json");
      var response = await Get<AllFeeQuotesViewModelGet>(
        client, MapiServer.ApiAggregatorAllFeeQuotesUrl, HttpStatusCode.OK);
      AssertIsOK(response);
      CheckSlaLevel(response.Miner.Single(), 0);
    }

    [TestMethod]
    public async Task GetAllFeeQuotes_WithSLA1()
    {
      GatewayRepository.DeleteGateway(1); // no gateways

      // standard: miningFee matches lv1, relayFee matches lv2 -> SLA1
      // data: miningFee matches lv2, relayFee matches lv1 -> SLA1
      CreateGateway("feeQuotesSLA1.json");
      var response = await Get<AllFeeQuotesViewModelGet>(
        client, MapiServer.ApiAggregatorAllFeeQuotesUrl, HttpStatusCode.OK);
      AssertIsOK(response);
      CheckSlaLevel(response.Miner.Single(), 1);
    }

    [TestMethod]
    public async Task GetAllFeeQuotes_WithSLA2()
    {
      GatewayRepository.DeleteGateway(1); // no gateways

      // all fees are big and match sla2
      CreateGateway("feeQuotesSLA2.json"); 
      var response = await Get<AllFeeQuotesViewModelGet>(
        client, MapiServer.ApiAggregatorAllFeeQuotesUrl, HttpStatusCode.OK);
      AssertIsOK(response);
      CheckSlaLevel(response.Miner.Single(), 2);
    }

    void AssertIsOKQueryTransactionResult(SignedPayloadViewModel[] response, string txId)
    {
      Assert.IsNotNull(response);
      foreach (var signedPayload in response)
      {
        Assert.IsNotNull(signedPayload.Payload);
        var payload = signedPayload.ExtractPayload<QueryTransactionStatusResponseViewModel>();
        Assert.AreEqual("1.2.0", payload.ApiVersion);
        Assert.IsTrue((MockedClock.UtcNow - payload.Timestamp).TotalSeconds < 60);
        Assert.AreEqual(txId, payload.Txid);
      }
    }

    [TestMethod]
    public async Task QueryTransactionStatus()
    {
      var response = await Get<SignedPayloadViewModel[]>(
        client, MapiServer.ApiAggregatorQueryTransactionStatusUrl + txC0Hash, HttpStatusCode.OK);
      AssertIsOKQueryTransactionResult(response, txC0Hash);

      GatewayRepository.DeleteGateway(1); 
      // no gateways - call must fail
      response = await Get<SignedPayloadViewModel[]>(
        client, MapiServer.ApiAggregatorQueryTransactionStatusUrl + txC0Hash, HttpStatusCode.InternalServerError);
      Assert.IsNull(response);
    }

    [TestMethod]
    public async Task QueryTransactionStatus_TestUnreachableGateway()
    {
      CreateGateway(reachable: false);
      // call must succeed, since we have one reachable and one unreachable gateway
      var response = await Get<SignedPayloadViewModel[]>(
        client, MapiServer.ApiAggregatorQueryTransactionStatusUrl + txC0Hash, HttpStatusCode.OK);
      AssertIsOKQueryTransactionResult(response, txC0Hash);

      // remove reachable gateway
      GatewayRepository.DeleteGateway(1);
      // call must fail
      response = await Get<SignedPayloadViewModel[]>(
        client, MapiServer.ApiAggregatorQueryTransactionStatusUrl + txC0Hash, HttpStatusCode.ServiceUnavailable);
      Assert.IsNull(response);
    }

    [TestMethod]
    public async Task QueryTransactionStatus_WithoutAccount()
    {
      AccountRepositoryPostgres.EmptyRepository(DbConnectionString);
      var response = await Get<SignedPayloadViewModel[]>(
        client, MapiServer.ApiAggregatorQueryTransactionStatusUrl + txC0Hash, HttpStatusCode.Unauthorized);
      Assert.IsNull(response);
    }

    [TestMethod]
    public async Task QueryTransactionStatus_WithoutSubscription()
    {
      var response = await QueryTransactionStatusWithoutSubscriptionAsync();
      Assert.IsNull(response);
    }

    [TestMethod]
    public async Task QueryTransactionStatus_WithInvalidAuthentication()
    {
      var response = await QueryTransactionStatusWithInvalidAuthenticationAsync();
      Assert.IsNull(response);
      RestAuthentication = null;

      ApiKeyAuthentication = AppSettings.RestAdminAPIKey;
      response = await Get<SignedPayloadViewModel[]>(
        client, MapiServer.ApiAggregatorQueryTransactionStatusUrl + txC0Hash, HttpStatusCode.Unauthorized);
      Assert.IsNull(response);
    }

    [TestMethod]
    public async Task QueryTransactionStatus_DifferentReturnResult()
    {
      // call is successful, but returnResult is failure
      var response = await Get<SignedPayloadViewModel[]>(
        client, MapiServer.ApiAggregatorQueryTransactionStatusUrl + txC0Hash, HttpStatusCode.OK);
      AssertIsOKQueryTransactionResult(response, txC0Hash);
      var payload = response.Single().ExtractPayload<QueryTransactionStatusResponseViewModel>();
      Assert.AreEqual("failure", payload.ReturnResult);

      var url = CreateGateway(url: "http://hostsuccess:1234/");
      ApiGatewayClientMock.AddUrlWithSuccessTx(url, txC0Hash);
      // now we should also have returnResult success
      response = await Get<SignedPayloadViewModel[]>(
            client, MapiServer.ApiAggregatorQueryTransactionStatusUrl + txC0Hash, HttpStatusCode.OK);
      AssertIsOKQueryTransactionResult(response, txC0Hash);
      var payloads = response.Select(x => x.ExtractPayload<QueryTransactionStatusResponseViewModel>());
      Assert.AreEqual(2, payloads.Count());
      Assert.AreEqual(1, payloads.Count(x => x.ReturnResult == "failure"));
      Assert.AreEqual(1, payloads.Count(x => x.ReturnResult == "success"));
    }

    [TestMethod]
    public async Task SubmitTransaction()
    {
      var reqContent = new StringContent($"{{ \"rawtx\": \"{txC0Hex}\" }}");
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);
      var response = await Post<SignedPayloadViewModel[]>(
        MapiServer.ApiAggregatorSubmitTransaction, client, reqContent, HttpStatusCode.OK);
      AssertIsOKSubmitTransactionResult(response.response, txC0Hash, "success");

      // Submit transaction that fails but we should get OK response. Failure is inside response payload.
      reqContent = new StringContent($"{{ \"rawtx\": \"{txC2Hex}\" }}");
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);
      response = await Post<SignedPayloadViewModel[]>(
        MapiServer.ApiAggregatorSubmitTransaction, client, reqContent, HttpStatusCode.OK);
      AssertIsOKSubmitTransactionResult(response.response, txC2Hash, "failure");

      // Remove gateway
      GatewayRepository.DeleteGateway(1);
      // no gateways - call must fail
      response = await Post<SignedPayloadViewModel[]>(
        MapiServer.ApiAggregatorSubmitTransaction, client, reqContent, HttpStatusCode.InternalServerError);
      Assert.IsNull(response.response);
    }

    [TestMethod]
    public async Task SubmitTransaction_WithInvalidAuthentication()
    {
      var response = await SubmitTransactionWithInvalidAuthenticationAsync(txC0Hex);
      Assert.IsNull(response);
      RestAuthentication = null;

      ApiKeyAuthentication = AppSettings.RestAdminAPIKey;

      var reqContent = new StringContent($"{{ \"rawtx\": \"{txC0Hex}\" }}");
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);
      (response, _) = await Post<SignedPayloadViewModel[]>(
        MapiServer.ApiAggregatorSubmitTransaction, client, reqContent, HttpStatusCode.Unauthorized);
      Assert.IsNull(response);
    }

    [TestMethod]
    public async Task SubmitTransaction_WithoutAccount()
    {
      AccountRepositoryPostgres.EmptyRepository(DbConnectionString);
      var reqContent = new StringContent($"{{ \"rawtx\": \"{txC0Hex}\" }}");
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);
      var response = await Post<SignedPayloadViewModel[]>(
        MapiServer.ApiAggregatorSubmitTransaction, client, reqContent, HttpStatusCode.Unauthorized);
      Assert.IsNull(response.response);
    }

    [TestMethod]
    public async Task SubmitTransaction_WithoutSubscription()
    {
      var response = await SubmitTransactionWithoutSubscriptionAsync(txC0Hex);
      Assert.IsNull(response);
    }

    [TestMethod]
    public async Task SubmitTransaction_TestUnreachableGateway()
    {
      CreateGateway(reachable: false);
      // call must succeed, since we have one reachable and one unreachable gateway
      var reqContent = new StringContent($"{{ \"rawtx\": \"{txC0Hex}\" }}");
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);
      var response = await Post<SignedPayloadViewModel[]>(
        MapiServer.ApiAggregatorSubmitTransaction, client, reqContent, HttpStatusCode.OK);
      AssertIsOKSubmitTransactionResult(response.response, txC0Hash, "success");

      // remove reachable gateway
      GatewayRepository.DeleteGateway(1);
      // call must fail
      response = await Post<SignedPayloadViewModel[]>(
        MapiServer.ApiAggregatorSubmitTransaction, client, reqContent, HttpStatusCode.ServiceUnavailable);
      Assert.IsNull(response.response);
    }

    [TestMethod]
    public async Task SubmitTransactionBinary()
    {
      var txBytes = HelperTools.HexStringToByteArray(txC0Hex);

      var reqContent = new ByteArrayContent(txBytes);
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Octet);

      var response =
        await Post<SignedPayloadViewModel[]>(MapiServer.ApiAggregatorSubmitTransaction, client, reqContent, HttpStatusCode.OK);

      AssertIsOKSubmitTransactionResult(response.response, txC0Hash, "success");
    }

    void AssertIsOKSubmitTransactionResult(SignedPayloadViewModel[] response, string txId, string expectedResult)
    {
      Assert.IsNotNull(response);
      foreach (var signedPayload in response)
      {
        Assert.IsNotNull(signedPayload.Payload);
        var payload = signedPayload.ExtractPayload<SubmitTransactionResponseViewModel>();
        Assert.IsTrue((MockedClock.UtcNow - payload.Timestamp).TotalSeconds < 60);
        Assert.AreEqual(txId, payload.Txid);
        Assert.AreEqual(expectedResult, payload.ReturnResult);
      }
    }

    [TestMethod]
    public async Task SubmitTransactions()
    {
      // Submit two valid transaction
      var reqContent = new StringContent($"[{{ \"rawtx\": \"{txC0Hex}\" }}, {{ \"rawtx\": \"{txC1Hex}\" }}]");
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);
      var response = await Post<SignedPayloadViewModel[]>(
        MapiServer.ApiAggregatorSubmitTransactions, client, reqContent, HttpStatusCode.OK);
      Assert.IsNotNull(response.response);
      AssertIsOKSubmitTransactionsResult(response.response, new (string, string)[] { (txC0Hash, "success"), (txC1Hash, "success") });

      // Submit transaction where one transaction fails
      reqContent = new StringContent($"[{{ \"rawtx\": \"{txC0Hex}\" }}, {{ \"rawtx\": \"{txC2Hex}\" }}]");
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);
      response = await Post<SignedPayloadViewModel[]>(
        MapiServer.ApiAggregatorSubmitTransactions, client, reqContent, HttpStatusCode.OK);
      Assert.IsNotNull(response.response);
      AssertIsOKSubmitTransactionsResult(response.response, new (string, string)[] { (txC0Hash, "success"), (txC2Hash, "failure") });

      // Submit transaction where both transaction fail
      reqContent = new StringContent($"[{{ \"rawtx\": \"{txC2Hex}\" }}, {{ \"rawtx\": \"{txC3Hex}\" }}]");
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);
      response = await Post<SignedPayloadViewModel[]>(
        MapiServer.ApiAggregatorSubmitTransactions, client, reqContent, HttpStatusCode.OK);
      Assert.IsNotNull(response.response);
      AssertIsOKSubmitTransactionsResult(response.response, new (string, string)[] { (txC2Hash, "failure"), (txC3Hash, "failure") });

      // Remove gateway
      GatewayRepository.DeleteGateway(1);
      // no gateways - call must fail
      response = await Post<SignedPayloadViewModel[]>(
        MapiServer.ApiAggregatorSubmitTransactions, client, reqContent, HttpStatusCode.InternalServerError);
      Assert.IsNull(response.response);
    }

    [TestMethod]
    public async Task SubmitTransactions_WithInvalidAuthentication()
    {
      var response = await SubmitTransactionWithInvalidAuthenticationAsync(txC0Hex);
      Assert.IsNull(response);
      RestAuthentication = null;

      ApiKeyAuthentication = AppSettings.RestAdminAPIKey;

      var reqContent = new StringContent($"[{{ \"rawtx\": \"{txC0Hex}\" }}, {{ \"rawtx\": \"{txC1Hex}\" }}]");
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);
      (response, _) = await Post<SignedPayloadViewModel[]>(
        MapiServer.ApiAggregatorSubmitTransactions, client, reqContent, HttpStatusCode.Unauthorized);
      Assert.IsNull(response);
    }

    [TestMethod]
    public async Task SubmitTransactions_WithoutAccount()
    {
      AccountRepositoryPostgres.EmptyRepository(DbConnectionString);
      var reqContent = new StringContent($"[{{ \"rawtx\": \"{txC0Hex}\" }}, {{ \"rawtx\": \"{txC1Hex}\" }}]");
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);
      var response = await Post<SignedPayloadViewModel[]>(
        MapiServer.ApiAggregatorSubmitTransactions, client, reqContent, HttpStatusCode.Unauthorized);
      Assert.IsNull(response.response);
    }

    [TestMethod]
    public async Task SubmitTransactions_WithoutSubscription()
    {
      var response = await SubmitTransactionsWithoutSubscriptionAsync(new string[] { txC0Hex, txC1Hex, txC2Hex, txC3Hex });
      Assert.IsNull(response);
    }

    [TestMethod]
    public async Task SubmitTransactions_TestUnreachableGateway()
    {
      CreateGateway(reachable: false);
      // call must succeed, since we have one reachable and one unreachable gateway
      var reqContent = new StringContent($"[{{ \"rawtx\": \"{txC0Hex}\" }}, {{ \"rawtx\": \"{txC1Hex}\" }}]");
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);
      var response = await Post<SignedPayloadViewModel[]>(
        MapiServer.ApiAggregatorSubmitTransactions, client, reqContent, HttpStatusCode.OK);
      Assert.IsNotNull(response.response);
      AssertIsOKSubmitTransactionsResult(response.response, new (string, string)[] { (txC0Hash, "success"), (txC1Hash, "success") });

      // remove reachable gateway
      GatewayRepository.DeleteGateway(1);
      // call must fail
      response = await Post<SignedPayloadViewModel[]>(
        MapiServer.ApiAggregatorSubmitTransactions, client, reqContent, HttpStatusCode.ServiceUnavailable);
      Assert.IsNull(response.response);
    }

    [TestMethod]
    public async Task SubmitTransactionsBinary()
    {
      var bytes = HelperTools.HexStringToByteArray(txC0Hex + txC1Hex + txC2Hex + txC3Hex);

      var reqContent = new ByteArrayContent(bytes);
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Octet);
      var response = await Post<SignedPayloadViewModel[]>(MapiServer.ApiAggregatorSubmitTransactions, client, reqContent, HttpStatusCode.OK);

      AssertIsOKSubmitTransactionsResult(response.response, new (string, string)[] { (txC0Hash, "success"), (txC1Hash, "success"), (txC2Hash, "failure"), (txC3Hash, "failure") });
    }

    void AssertIsOKSubmitTransactionsResult(SignedPayloadViewModel[] response, (string, string)[] txIdAndResult)
    {
      Assert.IsNotNull(response);
      foreach (var signedPayload in response)
      {
        Assert.IsNotNull(signedPayload.Payload);
        var payload = signedPayload.ExtractPayload<SubmitTransactionsResponseViewModel>();
        Assert.IsTrue((MockedClock.UtcNow - payload.Timestamp).TotalSeconds < 60);
        foreach(var payloadTx in payload.Txs)
        {
          var expectedResult = txIdAndResult.FirstOrDefault(x => x.Item1 == payloadTx.Txid);
          Assert.IsNotNull(expectedResult);
          Assert.AreEqual(expectedResult.Item2, payloadTx.ReturnResult);
        }
      }
    }

  }
}
