// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.Common.Clock;
using MerchantAPI.PaymentAggregator.Domain.Models;
using MerchantAPI.PaymentAggregator.Domain.ViewModels;
using MerchantAPI.PaymentAggregator.Rest.ViewModels;
using MerchantAPI.PaymentAggregator.Test.Functional.Mock;
using MerchantAPI.PaymentAggregator.Test.Functional.Server;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Threading.Tasks;

namespace MerchantAPI.PaymentAggregator.Test.Functional
{
  [TestClass]
  public class AggregatorTestBase: TestBase
  {
    private Account CreateAccount()
    {
      var create = new Account
      {
        ContactFirstName = "Name1",
        ContactLastName = "Name2",
        Email = "test@crea.si",
        Identity = "5",
        OrganisationName = "Organisation",
        IdentityProvider = "http://mysite.com"
      };
      var account = AccountRepository.AddAccountAsync(create).Result;
      if (account == null)
      {
        throw new Exception("Can not create account.");
      }
      return account;
    }

    private async void CreateSubscriptionAsync(int accountId, string serviceType)
    {
      _ = await SubscriptionRepository.AddSubscriptionAsync(accountId, serviceType, MockedClock.UtcNow);
    }

    protected string CreateGateway(string filenameJson = "feeQuotesWithIdentity.json", string url = "http://host:1234/", bool reachable = true)
    {
      // "http://host:1234/" - example of valid url
      url = ApiGatewayClientMock.CreateUrl(url, filenameJson, reachable);
      var gateway =
        new Gateway(1, url, "someMinerRef", "some@Email.com", "someOrganisation",
        "someContactFirstName", "someContactLastName", "Some remarks", MockedClock.UtcNow, null);
      GatewayRepository.CreateGateway(gateway);
      return url;
    }

    private void InsertServiceLevels()
    {
      var sls = ServiceLevelRest.GetServiceLevelArrayViewModelCreate().ToDomainObject();
      ServiceLevelRepository.InsertServiceLevelsAsync(sls.ServiceLevels).Wait();
    }

    public const string txC0Hash = "19e8d6172493b899bdadfd1e012e042a708b0844b388a18f6903586e9747a709";
    public const string txC0Hex =
      "0100000001878e9e24013c7f44d72dcd23d13cb542810742763c73d18fe4e66041541d2fc4000000006c493046022100bc9f9757625cb05fec40372e8df159c95a5a6984e0d9b915f059c4956e9a4e6d022100edfc8f5a508a255123218d5930c4a3180df4bd859c9fd57b0dc77c51bf154268012102ab4e425640983e035661216fec925e1c7cb6fceccf112a9aaeec1b1f75d69f9cffffffff02b00f6f06000000001976a9144f416f5f3049dd13b6a1b479b7d80e7d17e0f29788ac80f0fa02000000001976a91470b71d2a9295048934246c7e6678fa2838edd53688ac00000000";

    public const string txC1Hash = "9ce8e56fc0ab1b673b4fb7a092b805414685140be9bb70e3f8a2eb3d4f9c1105";
    public const string txC1Hex =
      "010000000109a747976e5803698fa188b344088b702a042e011efdadbd99b8932417d6e819000000006b48304502200bd4907de00d13a86aad4fe5f44796e3234ed7af7bc8d21a6da142e956c906160221008dc182f5f7df8ff64edde362db47c0819492ff4b4f95bb9b9607c293ec3f6843012102a4eea0a9ceb6f859a59268e050ac9212362b13d65dd0f53f27ce1de7cfd037a3ffffffff02409e9903000000001976a914326ed7cabfe3e5e037541b9c1f45e7b6aed0d71088ac7071d502000000001976a914d6e343ca15707dedfdfdb59303901a79eccead9788ac00000000";

    public const string txC2Hash = "a07dbd3c8165add674edb68bf9c8b48272135b6a35b9dddd117d00a52995347c";
    public const string txC2Hex =
      "010000000105119c4f3deba2f8e370bbe90b1485464105b892a0b74f3b671babc06fe5e89c000000006a47304402201efd88a86fc6de114b0a55e75d48a653eedb03281a3f2b52950e8377ed0ddbbf022076e650e384620d0f32d8f25107c9e9e2e3325c2bd7ad339e92408111781bb2e00121030984e003984bfc084a7b8fe7adec740e47b3f48d0e28d1391846b2e9e6b248a2ffffffff0240443701000000001976a9141dbc11836e98dccd15bc092d88ff611015c41d0a88ac005a6202000000001976a91461f8d0abc4c919b0693030734f4d9d3ce424fef988ac00000000";

    public const string txC3Hash = "3c412d497cb5d83fff8270062e9fe6c1fba147eed156887081dddfcc117e854c";
    public const string txC3Hex =
      "01000000017c349529a5007d11ddddb9356a5b137282b4c8f98bb6ed74d6ad65813cbd7da0010000006b483045022100e0a4fb47b9ff8cab51bac9904a7462ea063d4ab588e197231b03cd699d79990602205b9beb6a31ade571f021a117a0bcd1afbc63a786ffc61af898abce402d84c1520121022cd68b60621f51af57f1c87e52e1b1f394584273d104dff2c2b80329115c39b0ffffffff0240420f00000000001976a914cc7ab903497dc6326c5e5135bba23f1a4653db2388acb0f05202000000001976a91461f8d0abc4c919b0693030734f4d9d3ce424fef988ac00000000";

    [TestInitialize]
    public virtual void TestInitialize()
    {
      Initialize(mockedServices: true);
      var account = CreateAccount();
      // subscribe to all serviceTypes
      foreach (var type in Consts.ServiceType.validServiceTypes)
      {
        CreateSubscriptionAsync(account.AccountId, type);
      }
      InsertServiceLevels();
      CreateGateway();

      RestAuthentication = "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiI1IiwibmJmIjoxNTk5NDExNDQzLCJleHAiOjE5MTQ3NzE0NDMsImlhdCI6MTU5OTQxMTQ0MywiaXNzIjoiaHR0cDovL215c2l0ZS5jb20iLCJhdWQiOiJodHRwOi8vbXlhdWRpZW5jZS5jb20ifQ.Z43NASAbIxMZrL2MzbJTJD30hYCxhoAs-8heDjQMnjM";
    }

    [TestCleanup]
    public virtual void TestCleanup()
    {
      Cleanup();
    }

    protected async Task<Subscription> GetSubscriptionId(string serviceType)
    {
      int accountId = (await AccountRepository.GetAccountByIdentityAsync(GetMockedIdentity.Identity, GetMockedIdentity.IdentityProvider)).AccountId;
      var subscriptions = await SubscriptionRepository.GetSubscriptionsAsync(accountId, true);
      Subscription subscription = subscriptions.Single(x => x.ServiceType == serviceType);
      subscription.AccountID = accountId; // we get accountId = 0 from database
      return subscription;
    }

    protected async Task<SignedPayloadViewModel[]> SubmitTransactionAsync(string txHex, HttpStatusCode expectedStatusCode)
    {
      // Send transaction
      var reqContent = new StringContent($"{{ \"rawtx\": \"{txHex}\" }}");
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);

      var response =
        await Post<SignedPayloadViewModel[]>(MapiServer.ApiAggregatorSubmitTransaction, client, reqContent, expectedStatusCode);

      return response.response;
    }

    protected async Task<SignedPayloadViewModel[]> SubmitTransactionsAsync(string[] txHex, HttpStatusCode expectedStatusCode)
    {
      // Send transaction
      var txJson = "[" + string.Join(",", txHex.Select(t => $"{{ \"rawtx\": \"{t}\" }}")) + "]";
      var reqContent = new StringContent(txJson);
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);

      var response =
        await Post<SignedPayloadViewModel[]>(MapiServer.ApiAggregatorSubmitTransactions, client, reqContent, expectedStatusCode);

      return response.response;
    }

    protected async Task<AllFeeQuotesViewModelGet> GetAllFeeQuotesWithoutSubscriptionAsync()
    {
      Subscription subscription = await GetSubscriptionId(Consts.ServiceType.allFeeQuotes);
      await SubscriptionRepository.DeleteSubscriptionAsync(subscription.AccountID, subscription.SubscriptionId);

      return await Get<AllFeeQuotesViewModelGet>(
        client, MapiServer.ApiAggregatorAllFeeQuotesUrl, HttpStatusCode.Unauthorized);
    }

    protected async Task<AllFeeQuotesViewModelGet> GetAllFeeQuotesWithInvalidAuthenticationAsync()
    {
      var token = new char[RestAuthentication.Length];
      RestAuthentication.CopyTo(0, token, 0, token.Length);
      RestAuthentication += "Invalid";
      var response = await Get<AllFeeQuotesViewModelGet>(
         client, MapiServer.ApiAggregatorAllFeeQuotesUrl, HttpStatusCode.Unauthorized);
      // reset original
      RestAuthentication = new string(token);
      return response;
    }

    protected async Task<SignedPayloadViewModel[]> QueryTransactionStatusWithoutSubscriptionAsync()
    {
      var subscription = await GetSubscriptionId(Consts.ServiceType.queryTx);
      await SubscriptionRepository.DeleteSubscriptionAsync(subscription.AccountID, subscription.SubscriptionId);

      return await Get<SignedPayloadViewModel[]>(
        client, MapiServer.ApiAggregatorQueryTransactionStatusUrl + txC0Hash, HttpStatusCode.Unauthorized);
    }

    public async Task<SignedPayloadViewModel[]> QueryTransactionStatusWithInvalidAuthenticationAsync()
    {
      var token = new char[RestAuthentication.Length];
      RestAuthentication.CopyTo(0, token, 0, token.Length);
      RestAuthentication += "Invalid";
      var response = await Get<SignedPayloadViewModel[]>(
        client, MapiServer.ApiAggregatorQueryTransactionStatusUrl + txC0Hash, HttpStatusCode.Unauthorized);
      // reset original
      RestAuthentication = new string(token);
      return response;
    }

    protected async Task<SignedPayloadViewModel[]> SubmitTransactionWithoutSubscriptionAsync(string txHash)
    {
      var subscription = await GetSubscriptionId(Consts.ServiceType.submitTx);
      await SubscriptionRepository.DeleteSubscriptionAsync(subscription.AccountID, subscription.SubscriptionId);
      return await SubmitTransactionAsync(txHash, HttpStatusCode.Unauthorized);
    }

    public async Task<SignedPayloadViewModel[]> SubmitTransactionWithInvalidAuthenticationAsync(string txHash)
    {
      var token = new char[RestAuthentication.Length];
      RestAuthentication.CopyTo(0, token, 0, token.Length);
      RestAuthentication += "Invalid";
      var response = await SubmitTransactionAsync(txHash, HttpStatusCode.Unauthorized);
      // reset original
      RestAuthentication = new string(token);
      return response;
    }

    protected async Task<SignedPayloadViewModel[]> SubmitTransactionsWithoutSubscriptionAsync(string[] txs)
    {
      var subscription = await GetSubscriptionId(Consts.ServiceType.submitTx);
      await SubscriptionRepository.DeleteSubscriptionAsync(subscription.AccountID, subscription.SubscriptionId);
      return await SubmitTransactionsAsync(txs, HttpStatusCode.Unauthorized);
    }

    public async Task<SignedPayloadViewModel[]> SubmitTransactionsWithInvalidAuthenticationAsync(string[] txs)
    {
      var token = new char[RestAuthentication.Length];
      RestAuthentication.CopyTo(0, token, 0, token.Length);
      RestAuthentication += "Invalid";
      var response = await SubmitTransactionsAsync(txs, HttpStatusCode.Unauthorized);
      // reset original
      RestAuthentication = new string(token);
      return response;
    }
  }
}
