// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain.Repositories;
using MerchantAPI.APIGateway.Test.Functional.Mock;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;

namespace MerchantAPI.APIGateway.Test.Functional.Server
{
  class APIGatewayTestsMockStartup : APIGatewayTestsMockWithDBInsertStartup
  {
    public APIGatewayTestsMockStartup(IConfiguration env, IWebHostEnvironment environment) : base(env, environment)
    {

    }

    public override void ConfigureServices(IServiceCollection services)
    {
      base.ConfigureServices(services);

      // We register fee repository as singleton, so that we can modify the fee filename in individual tests
      var serviceDescriptor = services.FirstOrDefault(descriptor => descriptor.ServiceType == typeof(IFeeQuoteRepository));
      services.Remove(serviceDescriptor);
      services.AddSingleton<IFeeQuoteRepository, FeeQuoteRepositoryMock>();

      serviceDescriptor = services.FirstOrDefault(descriptor => descriptor.ServiceType == typeof(ITxRepository));
      services.Remove(serviceDescriptor);
      services.AddSingleton<ITxRepository, TxRepositoryMock>();
    }
  }
}
