// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.APIGateway.Rest.Database;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using nChain.CreateDB;
using nChain.CreateDB.DB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Test.Functional.Database
{
  public class MerchantAPITestDbManager : IDbManager
  {
    private const string DB_MAPI = "APIGateway";
    private readonly CreateDB aggregatorTestDb;
    private readonly CreateDB aggregatorDb;

    public MerchantAPITestDbManager(ILogger<CreateDB> logger, IConfiguration configuration)
    {
      string scriptLocation = "..\\..\\..\\Database\\Scripts";
      // Fix path for non windows os
      if (Path.DirectorySeparatorChar != '\\')
        scriptLocation = scriptLocation.Replace('\\', Path.DirectorySeparatorChar);
      aggregatorTestDb = new CreateDB(logger, DB_MAPI, RDBMS.Postgres,
        configuration["ConnectionStrings:DBConnectionStringDDL"],
        configuration["ConnectionStrings:DBConnectionStringMaster"],
        scriptLocation
      );
      aggregatorDb = new CreateDB(logger, DB_MAPI, RDBMS.Postgres,
        configuration["ConnectionStrings:DBConnectionStringDDL"],
        configuration["ConnectionStrings:DBConnectionStringMaster"]
      );
    }

    public bool DatabaseExists()
    {
      return aggregatorDb.DatabaseExists();
    }

    public bool CreateDb(out string errorMessage, out string errorMessageShort)
    {
      if(aggregatorTestDb.CreateDatabase(out errorMessage, out errorMessageShort))
        return aggregatorDb.CreateDatabase(out errorMessage, out errorMessageShort);
      return false;
    }
  }
}
