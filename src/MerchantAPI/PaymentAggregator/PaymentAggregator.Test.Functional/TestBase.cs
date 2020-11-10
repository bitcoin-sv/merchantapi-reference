// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.PaymentAggregator.Domain;
using MerchantAPI.PaymentAggregator.Domain.Models;
using MerchantAPI.PaymentAggregator.Domain.Repositories;
using MerchantAPI.PaymentAggregator.Infrastructure.Repositories;
using MerchantAPI.PaymentAggregator.Test.Functional.Server;
using MerchantAPI.Common.EventBus;
using Microsoft.Extensions.DependencyInjection;
using MerchantAPI.Common.Test;
using Microsoft.AspNetCore.TestHost;
using MerchantAPI.Common.Test.Mock;
using MerchantAPI.Common;
using System;

namespace MerchantAPI.PaymentAggregator.Test.Functional
{
  public class TestBase: CommonTestRestBase<AppSettings>
  {
    public GatewayRepositoryPostgres GatewayRepository { get; private set; }
    public AccountRepositoryPostgres AccountRepository { get; private set; }
    public SubscriptionRepositoryPostgres SubscriptionRepository { get; private set; }
    public ServiceLevelRepositoryPostgres ServiceLevelRepository { get; private set; }
    public ServiceRequestRepositoryPostgres ServiceRequestRepository { get; private set; }

    protected FeeQuoteRepositoryMock feeQuoteRepositoryMock;
    private static double quoteExpiryMinutes = 10;

    public IGateways Gateways { get; private set; }

    public IEventBus EventBus { get; private set; }

    public override string LOG_CATEGORY { get { return "MerchantAPI.PaymentAggregator.Test.Functional"; } }
    public override string DbConnectionString { get { return Configuration["PaymentAggregatorConnectionStrings:DBConnectionString"]; } }

    public override void Initialize(bool mockedServices = false)
    {
      base.Initialize(mockedServices);
      // setup repositories
      GatewayRepository = server.Services.GetRequiredService<IGatewayRepository>() as GatewayRepositoryPostgres;
      AccountRepository = server.Services.GetRequiredService<IAccountRepository>() as AccountRepositoryPostgres;
      SubscriptionRepository = server.Services.GetRequiredService<ISubscriptionRepository>() as SubscriptionRepositoryPostgres;
      ServiceLevelRepository = server.Services.GetRequiredService<IServiceLevelRepository>() as ServiceLevelRepositoryPostgres;
      ServiceRequestRepository = server.Services.GetRequiredService<IServiceRequestRepository>() as ServiceRequestRepositoryPostgres;
      feeQuoteRepositoryMock = server.Services.GetRequiredService<IFeeQuoteRepository>() as FeeQuoteRepositoryMock;
      FeeQuoteRepositoryMock.quoteExpiryMinutes = quoteExpiryMinutes;

      // setup common services
      Gateways = server.Services.GetRequiredService<IGateways>();
      EventBus = server.Services.GetRequiredService<IEventBus>();
    }

    public override TestServer CreateServer(bool mockedServices, TestServer serverCallback, string dbConnectionString)
    {
      return new TestServerBase().CreateServer<MapiServer, PaymentAggregatorTestsStartup, MerchantAPI.PaymentAggregator.Rest.Startup>(mockedServices, serverCallback, dbConnectionString);
    }

    public override string GetBaseUrl()
    {
      throw new System.NotImplementedException();
    }

  }
}
