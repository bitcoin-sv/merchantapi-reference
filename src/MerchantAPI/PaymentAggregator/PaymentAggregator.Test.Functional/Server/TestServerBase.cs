// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.PaymentAggregator.Infrastructure.Repositories;
using MerchantAPI.Common.Test;

namespace MerchantAPI.PaymentAggregator.Test.Functional.Server
{
  public class TestServerBase : CommonTestServerBase
  {
    protected override void CleanRepositories(string dbConnectionString)
    {
      GatewayRepositoryPostgres.EmptyRepository(dbConnectionString);
      ServiceLevelRepositoryPostgres.EmptyRepository(dbConnectionString);
      AccountRepositoryPostgres.EmptyRepository(dbConnectionString);
    }
  }
}
