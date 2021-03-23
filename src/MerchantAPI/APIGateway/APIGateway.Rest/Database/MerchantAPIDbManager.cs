// Copyright (c) 2020 Bitcoin Association

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using nChain.CreateDB;
using nChain.CreateDB.DB;

namespace MerchantAPI.APIGateway.Rest.Database
{
  public class MerchantAPIDbManager : IDbManager
  {
    private const string DB_MAPI = "APIGateway";
    private readonly CreateDB mapiDb;

    public MerchantAPIDbManager(ILogger<CreateDB> logger, IConfiguration configuration)
    {

      mapiDb = new CreateDB(logger, DB_MAPI, RDBMS.Postgres,
        configuration["ConnectionStrings:DBConnectionStringDDL"],
        configuration["ConnectionStrings:DBConnectionStringMaster"]
      );
    }

    public bool DatabaseExists()
    {
      return mapiDb.DatabaseExists();
    }

    public bool CreateDb(out string errorMessage, out string errorMessageShort)
    {
      return mapiDb.CreateDatabase(out errorMessage, out errorMessageShort);
    }
  }
}
