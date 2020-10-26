// Copyright (c) 2020 Bitcoin Association

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;

namespace MerchantAPI.Common.Database
{
  public class CreateDB: ICreateDB
  {

    private readonly IConfiguration configuration;
    private readonly ILogger<Database> logger;

    public CreateDB(IConfiguration configuration, ILogger<Database> logger)
    {
      this.configuration = configuration;
      this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private void DisplayScriptFolderOrder(string projectName, RDBMS rdbms)
    {
      DBFolders dbFolders = new DBFolders(projectName, rdbms, logger);
      dbFolders.WriteFolderNames(logger);
    }


    public bool DoCreateDB(string projectName, RDBMS rdbms, out string errorMessage, out string errorMessageShort)
    {
      // expected db scripts hierarchy: [ApplicationName.Database]\Scripts\Postgres\
      DisplayScriptFolderOrder(projectName, rdbms);
      return new Database(projectName, rdbms, this.configuration, this.logger).CreateDatabase(out errorMessage, out errorMessageShort);
    }

    public bool DatabaseExists(string projectName, RDBMS rdbms)
    {
      return new Database(projectName, rdbms, this.configuration, this.logger).DatabaseExists();

    }

  }
}
