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
using MerchantAPI.Common.BitcoinRest;

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
