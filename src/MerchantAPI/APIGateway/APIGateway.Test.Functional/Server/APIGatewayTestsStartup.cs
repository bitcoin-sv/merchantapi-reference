// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MerchantAPI.APIGateway.Rest.Database;
using MerchantAPI.APIGateway.Test.Functional.Database;

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

      // use test implementation of IDbManager that uses test database
      services.AddTransient<IDbManager, MerchantAPITestDbManager>();

    }
  }
}
