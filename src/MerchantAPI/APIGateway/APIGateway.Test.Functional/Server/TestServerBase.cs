// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.APIGateway.Infrastructure.Repositories;
using MerchantAPI.Common.Test;

namespace MerchantAPI.APIGateway.Test.Functional.Server
{
  public class TestServerBase : CommonTestServerBase
  {
    protected override void CleanRepositories(string dbConnectionString)
    {
      NodeRepositoryPostgres.EmptyRepository(dbConnectionString);
      TxRepositoryPostgres.EmptyRepository(dbConnectionString);
      FeeQuoteRepositoryPostgres.EmptyRepository(dbConnectionString);
    }
  }
}
