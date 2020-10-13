// Copyright (c) 2020 Bitcoin Association

using Microsoft.Extensions.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MerchantAPI.Common.Database
{
  public class DBFolders
  {
    readonly string ProjectName;
    readonly string CurrentRDBMSRootFolder; // e.g. "Postgres"
    private const string ScriptsFolderName = "Scripts";

    // e.g. "merchantapi2\src\MerchantAPI\APIGateway\APIGateway.Database"  
    private string _ApplicationDbFolder;
    public string ApplicationDbFolder
    {
      get
      {
        if (_ApplicationDbFolder == null)
        {
          _ApplicationDbFolder = GetDatabaseSrcRoot();
        }
        return _ApplicationDbFolder;
      }
    }

    // e.g. "merchantapi2\src\MerchantAPI\APIGateway\APIGateway.Database\Scripts\Postgres"
    public string PathToScripts
    {
      get
      {
        return Path.Combine(ApplicationDbFolder, ScriptsFolderName, CurrentRDBMSRootFolder);
      }
    }

    public List<string> CreateDBFoldersToProcess { get; } = new List<string>(); // folders with name "00_CreateDB"

    public List<string> ScriptFoldersToProcess { get; } = new List<string>(); // folders with version name

    private readonly HashSet<string> _projectAndVersions = new HashSet<string>();

    public DBFolders(string projectName, RDBMS databaseType, ILogger logger)
    {
      ProjectName = projectName;
      CurrentRDBMSRootFolder = databaseType.ToString();

      if (!Directory.Exists(PathToScripts))
        throw new ApplicationException($"Folder { PathToScripts } with custom scripts does not exist.");

      // Example: "[ApplicationName.Database]\Scripts\Postgres\": Postgres folder contains createDB or version folders with scripts.
      ProcessProjectDirectory(logger, projectName, PathToScripts);
    }

    public void WriteFolderNames(ILogger logger)
    {
      logger.LogInformation("Application folder: " + ApplicationDbFolder);
      logger.LogInformation(" Folders for createDB:");
      foreach (string dbFolder in CreateDBFoldersToProcess)
      {
        logger.LogInformation(dbFolder);
      }
      logger.LogInformation(" Folders with scripts:");
      foreach (string scriptFolder in ScriptFoldersToProcess)
      {
        logger.LogInformation(scriptFolder);
      }
    }

    public string GetDatabaseSrcRoot()
    {
      string path = Directory.GetCurrentDirectory();
      string dbFolderName = GetRootDatabaseFolderName();

      for (int i = 0; i < 6; i++)
      {
        string testData = Path.Combine(path, dbFolderName);
        if (Directory.Exists(testData))
        {
          return testData;
        }
        path = Path.Combine(path, "..");
      }
      throw new Exception($"Can not find '{dbFolderName}' near location {Directory.GetCurrentDirectory()}");
    }


    private void ProcessProjectDirectory(ILogger logger, string projectName, string projectDirectoryName)
    {
      foreach (string versionDirectoryName in DirectoryHelper.GetDirectories(projectDirectoryName))
      {
        ProcessVersionDirectory(logger, projectName, versionDirectoryName);
      }
    }

    private void ProcessVersionDirectory(ILogger logger, string projectName, string versionDirectoryName)
    {
      // first process all folders, that are not "ProjectName"
      string tempProjectName;
      foreach (string projectDirectoryName in DirectoryHelper.GetDirectories(versionDirectoryName))
      {
        tempProjectName = GetLastDirectoryName(projectDirectoryName);
        if (projectName.ToLower() != tempProjectName.ToLower())
        {
          ProcessProjectDirectory(logger, tempProjectName, projectDirectoryName);
        }
      }

      // now process "ProjectName" folder
      string version = GetLastDirectoryName(versionDirectoryName);
      string projectAndVersion = (projectName + "#" + version).ToLower();
      if (version.ToLower() == "00_createdb")
      {
        if (!_projectAndVersions.Contains(projectAndVersion))
        {
          _projectAndVersions.Add(projectAndVersion);
          CreateDBFoldersToProcess.Add(Path.Combine(versionDirectoryName, projectName));
        }

        CheckFolderForFiles(logger, versionDirectoryName);
      }
      else if (IsVersionFolder(version))
      {
        if (!_projectAndVersions.Contains(projectAndVersion))
        {
          _projectAndVersions.Add(projectAndVersion);
          ScriptFoldersToProcess.Add(Path.Combine(versionDirectoryName, projectName));
        }

        CheckFolderForFiles(logger, versionDirectoryName);
      }
      else
      {
        // ignore this folder
        string warningMessage = $"Folder '{ versionDirectoryName }' and its scripts will be ignored.";

        logger.LogInformation("WARNING!!!!");
        logger.LogInformation(warningMessage);
      }

    }


    private void CheckFolderForFiles(ILogger logger, string folderName)
    {
      string[] files = System.IO.Directory.GetFiles(folderName, "*.*", SearchOption.TopDirectoryOnly);
      if (files.Length > 0)
      {
        string warningMessage = $"Folder '{ folderName }' contains files, that will not be executed:";
        logger.LogInformation("WARNING!!!!");
        logger.LogInformation(warningMessage);

        foreach (string fileName in files)
        {
          warningMessage = $" { fileName }";
          logger.LogInformation(warningMessage);
        }
      }
    }
    private bool IsVersionFolder(string versionDirectoryName)
    {
      return Int32.TryParse(versionDirectoryName, out _);
    }

    private string GetRootDatabaseFolderName()
    {
      return String.Join('.', ProjectName, "Database");
    }

    private string GetLastDirectoryName(string directoryName)
    {
      return Path.GetFileName(directoryName);
    }

  }
}
