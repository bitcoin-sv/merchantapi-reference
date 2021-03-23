// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.APIGateway.Domain.Repositories;
using MerchantAPI.Common.BitcoinRpc;
using MerchantAPI.APIGateway.Test.Functional.CleanUpTx;
using MerchantAPI.APIGateway.Test.Functional.Mock;
using MerchantAPI.Common.Clock;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using MerchantAPI.APIGateway.Rest.Database;
using MerchantAPI.APIGateway.Test.Functional.Database;

namespace MerchantAPI.APIGateway.Test.Functional.Server
{
  class APIGatewayTestsMockStartup : MerchantAPI.APIGateway.Rest.Startup
  {
    public APIGatewayTestsMockStartup(IConfiguration env, IWebHostEnvironment environment) : base(env, environment)
    {

    }

    public override void ConfigureServices(IServiceCollection services)
    {
      base.ConfigureServices(services);
      // replace IRpcClientFactory with mock version
      var serviceDescriptor = services.FirstOrDefault(descriptor => descriptor.ServiceType == typeof(IRpcClientFactory));
      services.Remove(serviceDescriptor);
      services.AddSingleton<IRpcClientFactory, RpcClientFactoryMock>();
      // We register  fee repository as singleton, so that we can modify the fee filename in individual tests
      serviceDescriptor = services.FirstOrDefault(descriptor => descriptor.ServiceType == typeof(IFeeQuoteRepository));
      services.Remove(serviceDescriptor);
      services.AddSingleton<IFeeQuoteRepository, FeeQuoteRepositoryMock>();

      // We register clock as singleton, so that we can set time in individual tests
      services.AddSingleton<IClock, MockedClock>();
      services.AddSingleton<CleanUpTxWithPauseHandlerForTest>();

      // use test implementation of IDbManager that uses test database
      services.AddTransient<IDbManager, MerchantAPITestDbManager>();

    }
  }
}
