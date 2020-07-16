// Copyright (c) 2020 Bitcoin Association

using System.Linq;
using MerchantAPI.APIGateway.Domain.Repositories;
using MerchantAPI.APIGateway.Test.Functional.Mock;
using MerchantAPI.Common.BitcoinRpc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MerchantAPI.APIGateway.Test.Functional.Server
{
  class APIGatewayTestsStartup : MerchantAPI.APIGateway.Rest.Startup
  {
    public APIGatewayTestsStartup(IConfiguration env, IWebHostEnvironment environment) : base(env, environment)
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
      services.AddSingleton<IFeeQuoteRepository, FeeQuoteRepositoryMock>();


    }
  }
}
