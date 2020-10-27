// Copyright (c) 2020 Bitcoin Association

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MerchantAPI.Common.Database
{
  public enum RDBMS
  {
    Postgres
  }
  public class Database
  {
    readonly string projectName;
    readonly RDBMS rdbms;
    readonly IDB database;
    readonly IConfiguration configuration;
    readonly ILogger<Database> logger;
    readonly string connectionString;
    readonly string connectionStringMaster;

    // used for scripts in CreateDB folder - user must have permission to create database
    private const string connectionStringMasterName = "DBConnectionStringMaster";
    // connectionStringName is by default used for scripts in version folder
    // it is possible to specify custom connectionstring with script name
    // (example of default: 01_scriptName, custom: 01_customConnectionString_scriptName)
    private const string connectionStringName = "DBConnectionString";
    private const string connectionStringPrefix = "ConnectionStrings:";

    public Database(string projectName, RDBMS rdbms, IConfiguration configuration, ILogger<Database> logger)
    {
      this.projectName = projectName;
      this.rdbms = rdbms;
      this.configuration = configuration;
      this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
      database = GetDatabase(rdbms);
      connectionString = GetConnectionStringFromConfig(connectionStringName);
      connectionStringMaster = GetConnectionStringFromConfig(connectionStringMasterName);
    }


    public bool CreateDatabase(out string errorMessage, out string errorMessageShort)
    {
      errorMessage = "";
      errorMessageShort = "";

      try
      {
        bool ok = true;

        DBFolders dbFolders = new DBFolders(projectName, rdbms, logger);
        // we first process CreateDBFoldersToProcess - but only if no database exists yet
        // we use masterConnectionString for these scripts, because we need stronger permissions to create database

        string errorMessageLocal = "";
        int i;
        for (i = 0; i < dbFolders.CreateDBFoldersToProcess.Count; i++)
        {
          ok = ProcessDBFolder(dbFolders.CreateDBFoldersToProcess[i], out errorMessageLocal, out errorMessageShort);
          if (!ok)
            break;
        }

        if (!ok)
        {
          errorMessage = $"Executing scripts from createDB folder '{ dbFolders.CreateDBFoldersToProcess[i] }' returned error: '{ errorMessageLocal }'.";
          if (i < dbFolders.CreateDBFoldersToProcess.Count)
          {
            errorMessage += Environment.NewLine + "Following createDB folders must still be processed: ";
            errorMessage = string.Join(Environment.NewLine, dbFolders.CreateDBFoldersToProcess.Skip(i).ToArray());
          }
          if (dbFolders.ScriptFoldersToProcess.Count > 0)
          {
            errorMessage += Environment.NewLine + "Following folders must still be processed: ";
            for (int j = 0; j < dbFolders.ScriptFoldersToProcess.Count; j++)
            {
              errorMessage += Environment.NewLine + dbFolders.ScriptFoldersToProcess[j];
            }
          }
          return false;
        }
        else
        {
          // after folders with createDB naming, process the other (version) folders - ScriptFoldersToProcess
          for (i = 0; i < dbFolders.ScriptFoldersToProcess.Count; i++)
          {
            ok = ProcessScriptFolder(dbFolders.ScriptFoldersToProcess[i], out errorMessageLocal, out errorMessageShort);
            if (!ok)
              break;
          }

          if (!ok)
          {
            errorMessage = $"Executing scripts from folder '{ dbFolders.ScriptFoldersToProcess[i] }' returned error: '{ errorMessageLocal }'.";
            if (i < dbFolders.ScriptFoldersToProcess.Count)
            {
              errorMessage += Environment.NewLine + "Following folders must still be processed: ";
              errorMessage = string.Join(Environment.NewLine, dbFolders.ScriptFoldersToProcess.Skip(i).ToArray());
            }
            return false;
          }
          else
          {
            return true;
          }
        }
      }
      catch (Exception e)
      {
        errorMessage = e.Message;
        errorMessage += Environment.NewLine + "StackTrace:" + Environment.NewLine + e.StackTrace;
        errorMessageShort = e.Message;
        return false;
      }
    }

    public bool DatabaseExists()
    {
      string databaseName = database.GetDatabaseName(connectionString);
      logger.LogInformation($"connectionString:{connectionString}");
      logger.LogInformation($"connectionStringMaster:{connectionStringMaster}");
      string connectionStringMasterPostgres = database.GetConnectionStringWithDefaultDatabaseName(connectionStringMaster);
      logger.LogInformation($"Trying to connect to DB: '{connectionStringMasterPostgres}'");
      return database.DatabaseExists(connectionStringMasterPostgres ?? connectionString, databaseName);
    }

    private string GetConnectionStringFromConfig(string connectionStringName)
    {
      return configuration[$"{connectionStringPrefix}{connectionStringName}"]; 
    }

    private bool ProcessDBFolder(string dbFolder, out string errorMessage, out string errorMessageShort)
    {
      errorMessage = "";
      errorMessageShort = "";

      try
      {
        // folder example: Scripts\Postgres\00_CreateDB\
        // if database does not exist yet, process createDb scripts
        bool databaseExists = DatabaseExists();

        if (!databaseExists)
        {
          if (!ExecuteScripts(dbFolder, true, out errorMessage, out errorMessageShort))
          {
            return false;
          }
        }

        database.GetCurrentVersion(projectName, connectionStringMaster, out int currentVersion, out bool _);
        if (currentVersion == -1)
        {
          // create Version table and set current version to '0'
          CreateVersionTable(projectName, connectionStringMaster);

          if (databaseExists)
          {
            logger.LogWarning("Table version added. If you want to execute scripts in 00_CreateDB, drop database first.");
          }
        }
        return true;
      }
      catch (Exception e)
      {
        errorMessage = e.Message;
        errorMessage += Environment.NewLine + "StackTrace:" + Environment.NewLine + e.StackTrace;
        errorMessageShort = e.Message;
        return false;
      }
    }

    private bool ProcessScriptFolder(string scriptFolder, out string errorMessage, out string errorMessageShort)
    {
      errorMessage = "";
      errorMessageShort = "";

      try
      {
        // folder example: Scripts\Postgres\01\

        int executeIntermediateVersionsFromVersion = -1; // for now we always execute everything in folder, can be set if needed (in config)
        int[] installedVersions;

        database.GetCurrentVersion(projectName, connectionStringMaster, out int currentVersion, out bool updating);
        installedVersions = new int[] { currentVersion };

        if (updating)
        {
          errorMessage = $"When updating database for project '{ projectName }' to new version { String.Join(",", installedVersions) } error was thrown." +
            "Before another update of database set the field UPDATING=0 in table VERSION with command:'UPDATE VERSION SET UPDATING = 0' or delete row to retry folder processing." +
            "(if you are using docker use command: docker exec -it [container name] psql -U merchant -a merchant_gateway -c 'UPDATE VERSION SET UPDATING = 0')";
          errorMessageShort = errorMessage;
          return false;
        }

        int newVersion = Int32.Parse(Path.GetFileName(scriptFolder));
        if (executeIntermediateVersionsFromVersion == -1 && installedVersions.Any(x => x < newVersion) ||
          executeIntermediateVersionsFromVersion > -1 && newVersion >= executeIntermediateVersionsFromVersion && !installedVersions.Any(x => x == newVersion))
        {
          database.StartUpdating(projectName, newVersion, connectionStringMaster);
          if (!ExecuteScripts(scriptFolder, false, out errorMessage, out errorMessageShort))
          {
            database.RemoveVersion(projectName, newVersion, connectionStringMaster);
            return false;
          }

          database.FinishUpdating(projectName, newVersion, connectionStringMaster);
        }

        return true;
      }
      catch (Exception e)
      {
        errorMessage = e.Message;
        errorMessage += Environment.NewLine + "StackTrace:" + Environment.NewLine + e.StackTrace;
        errorMessageShort = e.Message;
        return false;
      }
    }

    private void CreateVersionTable(string projectName, string connectionString)
    {
      database.CreateVersionTable(connectionString);
      // after create we insert row with version 0
      database.StartUpdating(projectName, 0, connectionString);
      database.FinishUpdating(projectName, 0, connectionString);
    }

    private bool ExecuteScripts(string scriptsFolder, bool useCreateDBStringName, out string errorMessage, out string errorMessageShort)
    {
      errorMessage = "";
      errorMessageShort = "";

      string dbgMessage = $"Execution of scripts from folder '{ scriptsFolder }'.";
      logger.LogInformation(dbgMessage);

      try
      {
        bool ok = true;
        string errorMessageScript = "";
        string[] files = GetScripts(scriptsFolder);

        // execute all scripts ...
        int i;
        for (i = 0; i < files.Length; i++)
        {
          ok = ExecuteScript(files[i], useCreateDBStringName, out errorMessageScript);
          if (!ok) break;
        }

        if (!ok)
        {
          errorMessage = $"Executing scripts from folder '{ files[i] }' returned error: '{ errorMessageScript }'.";
          errorMessageShort = errorMessageScript;

          if (i < files.Length)
          {
            errorMessage += Environment.NewLine + "Following files must still be processed: ";
            while (i < files.Length)
            {
              errorMessage += Environment.NewLine + files[i];
              i++;
            }
          }
          return false;
        }
        else
        {
          return true;
        }
      }
      catch (Exception e)
      {
        errorMessage = e.Message;
        errorMessage += Environment.NewLine + "StackTrace:" + Environment.NewLine + e.StackTrace;
        errorMessageShort = e.Message;
        return false;
      }
    }

    private bool ExecuteScript(string scriptFilename, bool useCreateDBStringName, out string errorMessage)
    {
      errorMessage = "";
      try
      {
        ExecuteSqlScript(scriptFilename, useCreateDBStringName);
        return true;
      }
      catch (Exception e)
      {
        errorMessage = e.ToString();

        return false;
      }
    }

    private string[] GetScripts(string scriptsFolder)
    {
      List<string> files = new List<string>();

      if (Directory.Exists(scriptsFolder))
      {
        files.AddRange(GetSqlAndTxtScripts(scriptsFolder));

        files.Sort(0, files.Count, new ScriptNameSorter());
      }

      return files.ToArray(); 
    }

    private static string[] GetSqlAndTxtScripts(string scriptsFolder)
    {
      List<string> files = new List<string>();

      string[] filesDDL = Directory.GetFiles(scriptsFolder, "*.ddl");
      string[] filesSQL = Directory.GetFiles(scriptsFolder, "*.sql");
      string[] filesTXT = Directory.GetFiles(scriptsFolder, "*.txt");

      files.AddRange(filesDDL);
      files.AddRange(filesSQL);
      files.AddRange(filesTXT);

      files.Sort(0, files.Count, new ScriptNameSorter());

      return files.ToArray();
    }

    private void ExecuteSqlScript(string filePath, bool useCreateDBStringName)
    {
      if (useCreateDBStringName)
      {
        ExecuteSqlScript(filePath, connectionStringMaster, createDB: useCreateDBStringName);
      }
      else
      {
        if (GetConnectionStringName(filePath, out string connectionStringName))
        {
          ExecuteSqlScript(filePath, connectionStringName);
        }
        else
        {
          throw new Exception($"Script { Path.GetFileName(filePath) } will be ignored because it does not follow naming convention.");
        }
      }
    }

    public bool GetConnectionStringName(string filePath, out string connectionStringName)
    {
      // 0101_ConnectionStringName_ScriptName.sql

      string fileName = Path.GetFileName(filePath);
      int nameStart = fileName.IndexOf("_") + 1;
      if (nameStart < 0 
          || (fileName.StartsWith("_")) // we ignore scripts that start with _
          )
      {
        connectionStringName = "";
        return false;
      }
      int nameLength = fileName.Substring(nameStart).IndexOf("_");
      if (nameLength < 0)
      {
        // if not specified use default connection string
        connectionStringName = Database.connectionStringName;
        return true;
      }
      connectionStringName = fileName.Substring(nameStart, nameLength);
      return true;
    }


    private void ExecuteSqlScript(string filePath, string connectionStringName, bool createDB=false)
    {
      string dbgMessage = $"Starting with execution of script: { filePath }.";
      logger.LogInformation(dbgMessage);

      int connectionTimeout = 30; 

      System.Text.Encoding encoding = DirectoryHelper.GetSqlScriptFileEncoding(filePath);
      string connectionString = GetConnectionStringFromConfig(connectionStringName);
      if (createDB)
      {
        connectionString = database.GetConnectionStringWithDefaultDatabaseName(connectionStringMaster);
      }
      database.ExecuteFileScript(connectionString, filePath, encoding, connectionTimeout, createDB);

    }

    private IDB GetDatabase(RDBMS rdbms)
    {
      if (rdbms == RDBMS.Postgres)
      {
        return new DBPostgresBL();
      }
      else
      {
        throw new Exception($"{rdbms} not supported");
      }
    }

  }

  class ScriptNameSorter : IComparer<string>
  {
    public int Compare(string scriptName1, string scriptName2)
    {
      // First part of name (to first '_') is number of file inside version folder.

      try
      {
        scriptName1 = new FileInfo(scriptName1).Name;
        scriptName2 = new FileInfo(scriptName2).Name;

        string sPrefix1 = scriptName1.Substring(0, scriptName1.IndexOf("_"));
        int prefix1 = Int32.Parse(sPrefix1);

        string sPrefix2 = scriptName2.Substring(0, scriptName2.IndexOf("_"));
        int prefix2 = Int32.Parse(sPrefix2);

        return prefix1.CompareTo(prefix2);
      }
      catch (Exception)
      {
        return scriptName1.CompareTo(scriptName2);
      }
    }
  }

  class VersionFolderNameSorter : IComparer<string>
  {
    public int Compare(string folderName1, string folderName2)
    {
      try
      {
        folderName1 = new DirectoryInfo(folderName1).Name;
        folderName2 = new DirectoryInfo(folderName2).Name;

        int version1 = Int32.Parse(folderName1);
        int version2 = Int32.Parse(folderName2);

        return version1.CompareTo(version2);
      }
      catch (Exception)
      {
        return folderName1.CompareTo(folderName2);
      }
    }
  }

}
