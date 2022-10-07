// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;
using MerchantAPI.APIGateway.Domain.Actions;
using MerchantAPI.APIGateway.Test.Functional.Mock;
using MerchantAPI.Common.Clock;
using MerchantAPI.Common.Test.Clock;

namespace MerchantAPI.APIGateway.Test.Functional.Server
{
  class APIGatewayTestsStartupMapiMock : APIGatewayTestsStartup
  {
    public APIGatewayTestsStartupMapiMock(IConfiguration env, IWebHostEnvironment environment) : base(env, environment)
    {

    }

    public override void ConfigureServices(IServiceCollection services)
    {
      base.ConfigureServices(services);

      var serviceDescriptor = services.FirstOrDefault(descriptor => descriptor.ServiceType == typeof(IMapi));
      services.Remove(serviceDescriptor);
      services.AddTransient<IMapi, MapiMock>();

      services.AddSingleton<IClock, MockedClock>();
    }
  }
}
