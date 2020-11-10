// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.Common.Clock;
using MerchantAPI.Common.ViewModels;
using MerchantAPI.PaymentAggregator.Domain.Models;
using MerchantAPI.PaymentAggregator.Rest.ViewModels;
using MerchantAPI.PaymentAggregator.Test.Functional.Mock.CleanUpServiceRequest;
using MerchantAPI.PaymentAggregator.Test.Functional.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace MerchantAPI.PaymentAggregator.Test.Functional
{
  [TestClass]
  public class ServiceRequestTest: AggregatorTestBase
  {

    int cleanUpServiceRequestAfterDays;
    readonly int cancellationTimeout = 30000; // 30 seconds
    CleanUpServiceRequestWithPauseHandlerForTest cleanUpServiceRequest;

    [TestInitialize]
    override public void TestInitialize()
    {
      base.TestInitialize();
      cleanUpServiceRequest = server.Services.GetRequiredService<CleanUpServiceRequestWithPauseHandlerForTest>();
      cleanUpServiceRequestAfterDays = AppSettings.CleanUpServiceRequestAfterDays;
    }

    [TestCleanup]
    override public void TestCleanup()
    {
      base.TestCleanup();
    }

    void AssertIsOKInsertedServiceRequest(ServiceRequest serviceRequest, int subscriptionId, int statusCode)
    {
      Assert.AreEqual(subscriptionId, serviceRequest.SubscriptionId);
      Assert.IsTrue((MockedClock.UtcNow - serviceRequest.Created).TotalSeconds < 10);
      Assert.AreEqual(statusCode, serviceRequest.ResponseCode);
      Assert.IsTrue(serviceRequest.ExecutionTimeMs >= 0);
      Assert.IsTrue(serviceRequest.ExecutionTimeMs < 5000);
    }

    [TestMethod]
    public async Task CheckServiceRequest_GetAllFeeQuotes_Invalid()
    {
      // check service request table is empty
      var serviceRequests = await ServiceRequestRepository.GetServiceRequestsAsync();
      Assert.AreEqual(0, serviceRequests.Length);

      await GetAllFeeQuotesWithoutSubscriptionAsync();
      // service request table must still be empty
      serviceRequests = await ServiceRequestRepository.GetServiceRequestsAsync();
      Assert.AreEqual(0, serviceRequests.Length);

      await GetAllFeeQuotesWithInvalidAuthenticationAsync();
      // service request table must still be empty
      serviceRequests = await ServiceRequestRepository.GetServiceRequestsAsync();
      Assert.AreEqual(0, serviceRequests.Length);
    }

    [TestMethod]
    public async Task CheckServiceRequest_GetAllFeeQuotes_StatusCodeOk()
    {
      Subscription subscription = await GetSubscriptionId(Consts.ServiceType.allFeeQuotes);
      // check service request table is empty
      var serviceRequests = await ServiceRequestRepository.GetServiceRequestsAsync();
      Assert.AreEqual(0, serviceRequests.Length);

      // make successful call
      _ = await Get<AllFeeQuotesViewModelGet>(
        client, MapiServer.ApiAggregatorAllFeeQuotesUrl, HttpStatusCode.OK);

      // check service request table
      serviceRequests = await ServiceRequestRepository.GetServiceRequestsAsync();
      Assert.AreEqual(1, serviceRequests.Length);
      AssertIsOKInsertedServiceRequest(serviceRequests.Single(), subscription.SubscriptionId, (int)HttpStatusCode.OK);
    }

    [TestMethod]
    public async Task CheckServiceRequest_GetAllFeeQuotes_StatusCodeNotOk()
    {
      Subscription subscription = await GetSubscriptionId(Consts.ServiceType.allFeeQuotes);

      // remove reachable gateway
      GatewayRepository.DeleteGateway(1);
      // call must fail
      _ = await Get<AllFeeQuotesViewModelGet>(
        client, MapiServer.ApiAggregatorAllFeeQuotesUrl, HttpStatusCode.InternalServerError);
      // check service request table
      var serviceRequests = await ServiceRequestRepository.GetServiceRequestsAsync();
      Assert.AreEqual(1, serviceRequests.Length);
      AssertIsOKInsertedServiceRequest(serviceRequests.Single(), subscription.SubscriptionId, (int)HttpStatusCode.InternalServerError);

      CreateGateway("feeQuotesNonePublic.json"); // only one gateway, no public feeQuotes 
      _ = await GetWithHttpResponseReturned<AllFeeQuotesViewModelGet>(
        client, MapiServer.ApiAggregatorAllFeeQuotesUrl, HttpStatusCode.NotFound);
      // check service request table
      serviceRequests = await ServiceRequestRepository.GetServiceRequestsAsync();
      Assert.AreEqual(2, serviceRequests.Length);
      AssertIsOKInsertedServiceRequest(serviceRequests.OrderByDescending(x => x.Created).First(), subscription.SubscriptionId, (int)HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task CheckServiceRequest_QueryTransactionStatus_Invalid()
    {
      // check service request table is empty
      var serviceRequests = await ServiceRequestRepository.GetServiceRequestsAsync();
      Assert.AreEqual(0, serviceRequests.Length);

      _ = await Get<SignedPayloadViewModel[]>(
         client, MapiServer.ApiAggregatorQueryTransactionStatusUrl + "invalidHash", HttpStatusCode.BadRequest);
      // service request table must still be empty
      serviceRequests = await ServiceRequestRepository.GetServiceRequestsAsync();
      Assert.AreEqual(0, serviceRequests.Length);

      await QueryTransactionStatusWithoutSubscriptionAsync();
      // service request table must still be empty
      serviceRequests = await ServiceRequestRepository.GetServiceRequestsAsync();
      Assert.AreEqual(0, serviceRequests.Length);

      await QueryTransactionStatusWithInvalidAuthenticationAsync();
      // service request table must still be empty
      serviceRequests = await ServiceRequestRepository.GetServiceRequestsAsync();
      Assert.AreEqual(0, serviceRequests.Length);
    }


    [TestMethod]
    public async Task CheckServiceRequest_QueryTransactionStatus_StatusCodeOk()
    {
      Subscription subscription = await GetSubscriptionId(Consts.ServiceType.queryTx);
      // check service request table is empty
      var serviceRequests = await ServiceRequestRepository.GetServiceRequestsAsync();
      Assert.AreEqual(0, serviceRequests.Length);

      // make successful call
      _ = await Get<SignedPayloadViewModel[]>(
        client, MapiServer.ApiAggregatorQueryTransactionStatusUrl + txC0Hash, HttpStatusCode.OK);

      // check service request table
      serviceRequests = await ServiceRequestRepository.GetServiceRequestsAsync();
      Assert.AreEqual(1, serviceRequests.Length);
      AssertIsOKInsertedServiceRequest(serviceRequests.Single(), subscription.SubscriptionId, (int)HttpStatusCode.OK);
    }

    [TestMethod]
    public async Task CheckServiceRequest_QueryTransactionStatus_StatusCodeNotOk()
    {
      Subscription subscription = await GetSubscriptionId(Consts.ServiceType.queryTx);

      // remove reachable gateway
      GatewayRepository.DeleteGateway(1);
      // call must fail
      _ = await Get<SignedPayloadViewModel[]>(
        client, MapiServer.ApiAggregatorQueryTransactionStatusUrl + txC0Hash, HttpStatusCode.InternalServerError);
      // check service request table
      var serviceRequests = await ServiceRequestRepository.GetServiceRequestsAsync();
      Assert.AreEqual(1, serviceRequests.Length);
      AssertIsOKInsertedServiceRequest(serviceRequests.Single(), subscription.SubscriptionId, (int)HttpStatusCode.InternalServerError);
    }

    [TestMethod]
    public async Task CheckServiceRequest_SubmitTransaction_Invalid()
    {
      // check service request table is empty
      var serviceRequests = await ServiceRequestRepository.GetServiceRequestsAsync();
      Assert.AreEqual(0, serviceRequests.Length);

      await SubmitTransactionWithoutSubscriptionAsync(txC0Hash);
      // service request table must still be empty
      serviceRequests = await ServiceRequestRepository.GetServiceRequestsAsync();
      Assert.AreEqual(0, serviceRequests.Length);

      await SubmitTransactionWithInvalidAuthenticationAsync(txC0Hash);
      // service request table must still be empty
      serviceRequests = await ServiceRequestRepository.GetServiceRequestsAsync();
      Assert.AreEqual(0, serviceRequests.Length);
    }

    [TestMethod]
    public async Task CheckServiceRequest_SubmitTransaction_StatusCodeOk()
    {
      Subscription subscription = await GetSubscriptionId(Consts.ServiceType.submitTx);
      // check service request table is empty
      var serviceRequests = await ServiceRequestRepository.GetServiceRequestsAsync();
      Assert.AreEqual(0, serviceRequests.Length);

      // make successful call
      await SubmitTransactionAsync(txC0Hex, HttpStatusCode.OK);

      // check service request table
      serviceRequests = await ServiceRequestRepository.GetServiceRequestsAsync();
      Assert.AreEqual(1, serviceRequests.Length);
      AssertIsOKInsertedServiceRequest(serviceRequests.Single(), subscription.SubscriptionId, (int)HttpStatusCode.OK);
    }

    [TestMethod]
    public async Task CheckServiceRequest_SubmitTransaction_StatusCodeNotOk()
    {
      Subscription subscription = await GetSubscriptionId(Consts.ServiceType.submitTx);

      // remove reachable gateway
      GatewayRepository.DeleteGateway(1);
      // call must fail
      await SubmitTransactionAsync(txC0Hex, HttpStatusCode.InternalServerError);

      // check service request table
      var serviceRequests = await ServiceRequestRepository.GetServiceRequestsAsync();
      Assert.AreEqual(1, serviceRequests.Length);
      AssertIsOKInsertedServiceRequest(serviceRequests.Single(), subscription.SubscriptionId, (int)HttpStatusCode.InternalServerError);
    }

    private async Task ResumeAndWaitForCleanup(Common.EventBus.EventBusSubscription<CleanUpServiceRequestTriggeredEvent> cleanUpTxTriggeredSubscription)
    {
      using CancellationTokenSource cts = new CancellationTokenSource(cancellationTimeout);
      await cleanUpServiceRequest.ResumeAsync(cts.Token);

      // wait for cleanUpTx service to finish execute
      await cleanUpTxTriggeredSubscription.ReadAsync(cts.Token);
    }

    private async Task FillServiceRequestWithSomeData()
    {
      // make successful calls
      await Get<SignedPayloadViewModel[]>(
        client, MapiServer.ApiAggregatorQueryTransactionStatusUrl + txC0Hash, HttpStatusCode.OK);
      await Get<AllFeeQuotesViewModelGet>(
        client, MapiServer.ApiAggregatorAllFeeQuotesUrl, HttpStatusCode.OK);
      await SubmitTransactionAsync(txC0Hex, HttpStatusCode.OK);
    }

    [TestMethod]
    public async Task CheckServiceRequest_NothingToCleanUp()
    {
      //arrange
      cleanUpServiceRequest.Pause();
      var cleanUpTxTriggeredSubscription = EventBus.Subscribe<CleanUpServiceRequestTriggeredEvent>();
      await FillServiceRequestWithSomeData();
      // check service request table
      var serviceRequests = await ServiceRequestRepository.GetServiceRequestsAsync();
      Assert.AreEqual(3, serviceRequests.Length);

      using (MockedClock.NowIs(DateTime.UtcNow.AddDays(cleanUpServiceRequestAfterDays-1)))
      {
        await ResumeAndWaitForCleanup(cleanUpTxTriggeredSubscription);
        // service request table must still have records - elapsed time since rows were created is under config limit
        serviceRequests = await ServiceRequestRepository.GetServiceRequestsAsync();
        Assert.AreEqual(3, serviceRequests.Length);
      }
    }

    [TestMethod]
    public async Task CheckServiceRequest_CleanUp()
    {
      //arrange
      cleanUpServiceRequest.Pause();
      var cleanUpTxTriggeredSubscription = EventBus.Subscribe<CleanUpServiceRequestTriggeredEvent>();
      await FillServiceRequestWithSomeData();
      // check service request table
      var serviceRequests = await ServiceRequestRepository.GetServiceRequestsAsync();
      Assert.AreEqual(3, serviceRequests.Length);

      using (MockedClock.NowIs(DateTime.UtcNow.AddDays(cleanUpServiceRequestAfterDays)))
      {
        await ResumeAndWaitForCleanup(cleanUpTxTriggeredSubscription);
        // service request table should be empty
        serviceRequests = await ServiceRequestRepository.GetServiceRequestsAsync();
        Assert.AreEqual(0, serviceRequests.Length);
      }
    }

  }
}
