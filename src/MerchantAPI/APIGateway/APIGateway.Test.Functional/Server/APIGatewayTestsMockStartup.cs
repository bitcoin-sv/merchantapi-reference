// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

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
using MerchantAPI.Common.Test.Clock;
using MerchantAPI.APIGateway.Domain.Models;
using MerchantAPI.APIGateway.Domain.Actions;

namespace MerchantAPI.APIGateway.Test.Functional.Server
{
  class APIGatewayTestsMockStartup : Rest.Startup
  {
    public APIGatewayTestsMockStartup(IConfiguration env, IWebHostEnvironment environment) : base(env, environment)
    {

    }

    public override void ConfigureServices(IServiceCollection services)
    {
      base.ConfigureServices(services);

      // replace IRpcClientFactory and IRestClientFactory with same instance of mock version
      var serviceDescriptor = services.FirstOrDefault(descriptor => descriptor.ServiceType == typeof(IRpcClientFactory));
      services.Remove(serviceDescriptor);

      var rpcClientFactoryMock = new RpcClientFactoryMock();
      services.AddSingleton<IRpcClientFactory>(rpcClientFactoryMock);

      serviceDescriptor = services.FirstOrDefault(descriptor => descriptor.ServiceType == typeof(IClock));
      services.Remove(serviceDescriptor);
      services.AddSingleton<IClock, MockedClock>();

      serviceDescriptor = services.FirstOrDefault(descriptor => descriptor.ServiceType == typeof(IZMQEndpointChecker));
      services.Remove(serviceDescriptor);
      services.AddTransient<IZMQEndpointChecker, MockZMQEndpointChecker>();

      // We register fee repository as singleton, so that we can modify the fee filename in individual tests
      serviceDescriptor = services.FirstOrDefault(descriptor => descriptor.ServiceType == typeof(IFeeQuoteRepository));
      services.Remove(serviceDescriptor);
      services.AddSingleton<IFeeQuoteRepository, FeeQuoteRepositoryMock>();

      // We register clock as singleton, so that we can set time in individual tests
      services.AddSingleton<IClock, MockedClock>();
      services.AddSingleton<CleanUpTxWithPauseHandlerForTest>();

      serviceDescriptor = services.FirstOrDefault(descriptor => descriptor.ServiceType == typeof(IMapi));
      services.Remove(serviceDescriptor);
      services.AddTransient<IMapi, MapiMock>();

      // use test implementation of IDbManager that uses test database
      services.AddTransient<IDbManager, MerchantAPITestDbManager>();

    }
  }
}
