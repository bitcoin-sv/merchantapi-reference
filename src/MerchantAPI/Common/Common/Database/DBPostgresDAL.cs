// Copyright (c) 2020 Bitcoin Association

using Dapper;
using Npgsql;
using System;
using System.IO;


namespace MerchantAPI.Common.Database
{
  class DBPostgresDAL
  {
    public void CreateVersionTable(string connectionString)
    {
      using var connection = new NpgsqlConnection(connectionString);
      RetryUtils.Exec(() => connection.Open());

      string createVersionTable = @"
      CREATE TABLE Version (
		      versionId		SERIAL		NOT NULL,
		      projectName		VARCHAR(256) NOT NULL,
		      updating			INTEGER			NOT NULL,
	       creationDate			TIMESTAMP NOT NULL,
	
		      PRIMARY KEY (versionid)
      );
      ALTER TABLE Version ADD CONSTRAINT version_versionIdAndProjectName UNIQUE (versionId,projectName);";

      connection.Execute(createVersionTable, null);
    }


    public bool DatabaseExists(string connectionString, string databaseName)
    {
      using var connection = new NpgsqlConnection(connectionString);
      string selectCommand = $"SELECT datname FROM pg_database WHERE datistemplate = false AND datname='{databaseName}'";
      bool databaseExist = false;

      try
      {
        string name = connection.QueryFirstOrDefault<string>(selectCommand);
        databaseExist = (name != null);
      }
      catch (Exception e)
      {
        string message = $"Error when checking for database existence (selectCommand='{ selectCommand }', connectionString='{ connectionString}') Origin error message: { e.Message }  Inner exception: {e.InnerException }";
        throw new Exception(message, e);
      }

      return databaseExist;
    }

    public void GetCurrentVersion(string projectName, string connectionString, out int currentVersion, out bool updating)
    {
      using var connection = new NpgsqlConnection(connectionString);

      try
      {
        // check if Version table exists (it is possible, that user doesn't have rights to access user_tables
        if (ExistsVersionTable(connectionString))
        {
          RetryUtils.Exec(() => connection.Open());
          string selectCommand = $"SELECT max(versionid) versionid, max(updating) updating FROM Version WHERE upper(projectname) = upper('{ projectName }') group by versionid having max(versionid) = (select max(versionid) from Version WHERE upper(projectname) = upper('{ projectName }'))";
          var version = connection.QueryFirstOrDefault<Version>(selectCommand);

          if (version == null)
          {
            // Version table has no rows
            currentVersion = -1;
            updating = false;
          }
          else
          {
            currentVersion = version.VersionId;
            updating = version.Updating == 1;
          }
        }
        else
        {
          // Version table does not exist
          currentVersion = -1;
          updating = false;
        }
      }
      catch (Exception e)
      {
        currentVersion = -1;
        updating = false;
        throw e;
      }

    }


    public bool ExistsVersionTable(string connectionString)
    {
      using var connection = new NpgsqlConnection(connectionString);
      string selectCommand = "SELECT * FROM VERSION";
      bool versionTableExists = false;

      try
      {

        var version = connection.Query<Version>(selectCommand);
        versionTableExists =  true;
      }
      catch (PostgresException e)
      {
        string errorMessage = e.Message.ToLower();
        
        if (!(e.SqlState=="42P01" && errorMessage.Contains("relation \"version\" does not exist")))
        {
          throw e;
        }
      }

      return versionTableExists;
    }

    public void ExecuteFileScript(string connectionString, string filepath, System.Text.Encoding encoding, int commandTimeout, bool createDB=false)
    {
      using var connection = new NpgsqlConnection(connectionString);
      string command = File.ReadAllText(filepath, encoding);

      RetryUtils.Exec(() => connection.Open());

      if (createDB) // create database cannot run inside a transaction block
      {
        connection.Execute(command, commandTimeout: commandTimeout);
      }
      else
      {
        using var transaction = connection.BeginTransaction();
        connection.Execute(command, transaction: transaction, commandTimeout: commandTimeout);
        transaction.Commit();
      }

    }  

    private void ExecuteNonQuery(string command, string connectionString, object parameters = null)
    {
      using var connection = new NpgsqlConnection(connectionString);
      RetryUtils.Exec(() => connection.Open());

      connection.Execute(command, param: parameters);
    }

    public void StartUpdating(string projectName, int newVersion, string connectionString)
    {
      ExecuteNonQuery(GetInsertIntoVersionSQL(), connectionString, new { newVersion, projectName });
    }

    public static string GetInsertIntoVersionSQL()
    {
      return $"INSERT INTO VERSION (VERSIONID, PROJECTNAME, UPDATING, CREATIONDATE) VALUES(@newVersion, @projectName, 1, timezone('utc', now()) ); ";
    }

    public void FinishUpdating(string projectName, int version, string connectionString)
    {
      string commandString = $"UPDATE VERSION SET UPDATING = 0 WHERE UPDATING = 1 AND VERSIONID = @version AND upper(projectname) = upper(@projectName) ";
      ExecuteNonQuery(commandString, connectionString, new { version, projectName});
    }

    public void RemoveVersion(string projectName, int version, string connectionString)
    {
      string commandString = $"DELETE FROM VERSION WHERE UPDATING = 1 AND VERSIONID = @version AND upper(projectname) = upper(@projectName) ";
      ExecuteNonQuery(commandString, connectionString, new { version, projectName });
    }

  }
}
