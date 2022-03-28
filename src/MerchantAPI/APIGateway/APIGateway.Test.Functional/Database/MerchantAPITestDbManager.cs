// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain;
using MerchantAPI.APIGateway.Rest.Database;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using nChain.CreateDB;
using nChain.CreateDB.DB;
using Npgsql;
using System.IO;

namespace MerchantAPI.APIGateway.Test.Functional.Database
{
  public class MerchantAPITestDbManager : IDbManager
  {
    private const string DB_MAPI = "APIGateway";
    private readonly CreateDB mapiTestDb;
    private readonly CreateDB mapiDb;

    public MerchantAPITestDbManager(ILogger<CreateDB> logger, IConfiguration configuration, IOptions<AppSettings> options)
    {
      var dbConnectionStringDDL = configuration["ConnectionStrings:DBConnectionStringDDL"];
      var dbConnectionStringMaster = configuration["ConnectionStrings:DBConnectionStringMaster"];
      var startupCommandTimeoutMinutes = options.Value.DbConnection.StartupCommandTimeoutMinutes;
      if (startupCommandTimeoutMinutes != null)
      {
        var connectionStringBuilder = new NpgsqlConnectionStringBuilder
        {
          ConnectionString = dbConnectionStringDDL
        };
        connectionStringBuilder.CommandTimeout = startupCommandTimeoutMinutes.Value * 60;
        dbConnectionStringDDL = connectionStringBuilder.ToString();
        connectionStringBuilder = new NpgsqlConnectionStringBuilder
        {
          ConnectionString = dbConnectionStringMaster
        };
        connectionStringBuilder.CommandTimeout = startupCommandTimeoutMinutes.Value * 60;
        dbConnectionStringMaster = connectionStringBuilder.ToString();
      }

      string scriptLocation = "..\\..\\..\\Database\\Scripts";
      // Fix path for non windows os
      if (Path.DirectorySeparatorChar != '\\')
        scriptLocation = scriptLocation.Replace('\\', Path.DirectorySeparatorChar);
      mapiTestDb = new CreateDB(logger, DB_MAPI, RDBMS.Postgres,
        dbConnectionStringDDL,
        dbConnectionStringMaster,
        scriptLocation
      );
      mapiDb = new CreateDB(logger, DB_MAPI, RDBMS.Postgres,
        dbConnectionStringDDL,
        dbConnectionStringMaster
      );
    }

    public bool DatabaseExists()
    {
      return mapiDb.DatabaseExists();
    }

    public bool CreateDb(out string errorMessage, out string errorMessageShort)
    {
      if(mapiTestDb.CreateDatabase(out errorMessage, out errorMessageShort))
        return mapiDb.CreateDatabase(out errorMessage, out errorMessageShort);
      return false;
    }
  }
}
