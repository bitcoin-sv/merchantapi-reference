// Copyright (c) 2020 Bitcoin Association

using Npgsql;
using System.Text;

namespace MerchantAPI.Common.Database
{
  class DBPostgresBL: IDB
  {

    public DBPostgresBL()
    {
    }

    public string GetDatabaseName(string connectionString)
    {
      var connectionStringBuilder = new NpgsqlConnectionStringBuilder
      {
        ConnectionString = connectionString
      };
      return connectionStringBuilder.Database;
    }

    public string GetConnectionStringWithDefaultDatabaseName(string connectionString)
    {
      string databaseName = "postgres";
      var connectionStringBuilder = new NpgsqlConnectionStringBuilder
      {
        ConnectionString = connectionString
      };
      connectionStringBuilder.Database = databaseName;
      return connectionStringBuilder.ConnectionString;
    }

    public void CreateVersionTable(string connectionString)
    {
      DBPostgresDAL db = new DBPostgresDAL();
      if (!db.ExistsVersionTable(connectionString))
      {
        db.CreateVersionTable(connectionString);
      }

    }

    public void ExecuteFileScript(string connectionString, string filepath, Encoding encoding, int fragmentTimeout, bool createDb=false)
    {
      DBPostgresDAL db = new DBPostgresDAL();
      db.ExecuteFileScript(connectionString, filepath, encoding, fragmentTimeout, createDb);
    }


    public bool DatabaseExists(string connectionStringMaster, string databaseName)
    {
      DBPostgresDAL db = new DBPostgresDAL();
      bool result = db.DatabaseExists(connectionStringMaster, databaseName);
      return result;
    }

    public void StartUpdating(string projectName, int newVersion, string connectionString)
    {
      DBPostgresDAL db = new DBPostgresDAL();
      db.StartUpdating(projectName, newVersion, connectionString);
    }

    public void FinishUpdating(string projectName, int version, string connectionString)
    {
      DBPostgresDAL db = new DBPostgresDAL();
      db.FinishUpdating(projectName, version, connectionString);
    }

    public void RemoveVersion(string projectName, int version, string connectionString)
    {
      DBPostgresDAL db = new DBPostgresDAL();
      db.RemoveVersion(projectName, version, connectionString);
    }

    public void GetCurrentVersion(string projectName, string connectionString, out int currentVersion, out bool updating)
    {
      DBPostgresDAL db = new DBPostgresDAL();
      if (!db.ExistsVersionTable(connectionString))
      {
        db.CreateVersionTable(connectionString);
      }
      db.GetCurrentVersion(projectName, connectionString, out currentVersion, out updating);
    }

  }
}
