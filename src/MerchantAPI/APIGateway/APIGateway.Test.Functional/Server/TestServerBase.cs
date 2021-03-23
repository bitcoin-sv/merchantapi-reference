// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.APIGateway.Infrastructure.Repositories;
using MerchantAPI.Common.Test;

namespace MerchantAPI.APIGateway.Test.Functional.Server
{
  public class TestServerBase : CommonTestServerBase
  {
    private string DBConnectionStringDDL { get; set; }
    public TestServerBase() : base() { }

    public TestServerBase(string dbConnectionStringDDL) : base()
    {
      DBConnectionStringDDL = dbConnectionStringDDL;
    }

    protected override void CleanRepositories(string dbConnectionString)
    {
      NodeRepositoryPostgres.EmptyRepository(DBConnectionStringDDL ?? dbConnectionString);
      TxRepositoryPostgres.EmptyRepository(DBConnectionStringDDL ?? dbConnectionString);
      FeeQuoteRepositoryPostgres.EmptyRepository(DBConnectionStringDDL ?? dbConnectionString);
    }
  }
}
