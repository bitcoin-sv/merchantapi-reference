// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using nChain.CreateDB;
using nChain.CreateDB.DB;
using nChain.CreateDB.Tools;
using System;
using System.IO;

namespace MerchantAPI.APIGateway.Rest.Database
{
  public class MerchantAPIDbManager : IDbManager
  {
    private const string DB_MAPI = "APIGateway";
    private readonly CreateDB mapiDb;
    readonly ILogger<CreateDB> logger;

    // Instances used for upgrade from v1.2.0
    private readonly CreateDB mapiDbNoMaster;
    private readonly CreateDB mapiDbUpgradeV12;

    public MerchantAPIDbManager(ILogger<CreateDB> logger, IConfiguration configuration)
    {
      this.logger = logger ?? throw new ArgumentNullException(nameof(logger));

      mapiDb = new CreateDB(logger, DB_MAPI, RDBMS.Postgres,
        configuration["ConnectionStrings:DBConnectionStringDDL"],
        configuration["ConnectionStrings:DBConnectionStringMaster"]
      );

      mapiDbNoMaster = new CreateDB(logger, DB_MAPI, RDBMS.Postgres,
        configuration["ConnectionStrings:DBConnectionStringDDL"]
      );

      var root = GetScriptsRootForUpgradeFromV12();
      mapiDbUpgradeV12 = new CreateDB(logger, DB_MAPI, RDBMS.Postgres,
        configuration["ConnectionStrings:DBConnectionStringMaster"],
        configuration["ConnectionStrings:DBConnectionStringMaster"],
        root
      );
    }

    public bool DatabaseExists()
    {
      return mapiDb.DatabaseExists();
    }

    public bool CreateDb(out string errorMessage, out string errorMessageShort)
    {
      // If only DDL connection fails then this database needs to be upgraded from mAPI 1.2
      if (ShouldUpgradeFromV12())
      {
        if (!UpgradeFromV12(out errorMessage, out errorMessageShort))
          return false;
      }
      return mapiDb.CreateDatabase(out errorMessage, out errorMessageShort);
    }

    private bool ShouldUpgradeFromV12()
    {
      bool shouldUpgrade = false;
      if (mapiDb.DatabaseExists())
      {
        try
        {
          mapiDbNoMaster.DatabaseExists();
        }
        catch (Exception)
        {
          shouldUpgrade = true;
        }
      }
      return shouldUpgrade;
    }

    private bool UpgradeFromV12(out string errorMessage, out string errorMessageShort)
    {
      logger.LogInformation("Upgrading database from mAPI version 1.2.0");

      return mapiDbUpgradeV12.CreateDatabase(out errorMessage, out errorMessageShort);
    }

    private static string GetScriptsRootForUpgradeFromV12()
    {
      string scriptsRoot = ScriptPathTools.FindScripts(DB_MAPI, RDBMS.Postgres);
      return Path.Combine(scriptsRoot, "Upgrade_v12_v13");     
    }
  }
}
