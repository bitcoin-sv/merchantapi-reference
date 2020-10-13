// Copyright (c) 2020 Bitcoin Association

using System.Text;

namespace MerchantAPI.Common.Database
{
  public interface IDB
  {
    string GetDatabaseName(string connectionString);
    string GetConnectionStringWithDefaultDatabaseName(string connectionString);
    bool DatabaseExists(string connectionString, string databaseName);
    void ExecuteFileScript(string connectionString, string filepath, Encoding encoding, int fragmentTimeout, bool createDB);
    void CreateVersionTable(string connectionString);
    void GetCurrentVersion(string projectName, string connectionString, out int currentVersion, out bool updating);
    void StartUpdating(string projectName, int newVersion, string connectionString);
    void FinishUpdating(string projectName, int version, string connectionString);
    void RemoveVersion(string projectName, int version, string connectionString);

  }
}
