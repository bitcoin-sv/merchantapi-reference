// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.Common.Clock;
using MerchantAPI.PaymentAggregator.Domain.Repositories;
using MerchantAPI.PaymentAggregator.Domain.Client;
using MerchantAPI.PaymentAggregator.Test.Functional.Mock;
using MerchantAPI.PaymentAggregator.Test.Functional.Mock.CleanUpServiceRequest;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;

namespace MerchantAPI.PaymentAggregator.Test.Functional.Server
{
  class PaymentAggregatorTestsStartup : MerchantAPI.PaymentAggregator.Rest.Startup
  {
    public PaymentAggregatorTestsStartup(IConfiguration env, IWebHostEnvironment environment) : base(env, environment)
    {

    }

    public override void ConfigureServices(IServiceCollection services)
    {
      base.ConfigureServices(services);

      // replace IApiGatewayClientFactory with mock version
      var serviceDescriptor = services.FirstOrDefault(descriptor => descriptor.ServiceType == typeof(IApiGatewayClientFactory));
      services.Remove(serviceDescriptor);
      services.AddSingleton<IApiGatewayClientFactory, ApiGatewayClientMockFactory>();

      // We register clock as singleton, so that we can set time in individual tests
      services.AddSingleton<IClock, MockedClock>();
      services.AddSingleton<CleanUpServiceRequestWithPauseHandlerForTest>();
      // We register  fee repository as singleton, so that we can modify the fee filename in individual tests
      services.AddSingleton<IFeeQuoteRepository, FeeQuoteRepositoryMock>();
    }
  }
}
