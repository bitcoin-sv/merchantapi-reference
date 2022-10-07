// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain;
using MerchantAPI.APIGateway.Rest.ViewModels.Faults;
using MerchantAPI.APIGateway.Test.Functional.Server;
using MerchantAPI.Common.Test;
using Microsoft.AspNetCore.TestHost;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Test.Functional
{
  [TestCategory("TestCategoryNo1")]
  [TestClass]
  public class FaultRest : CommonRestMethodsBase<FaultTriggerViewModelGet, FaultTriggerViewModelCreate, AppSettings>
  {
    public override string LOG_CATEGORY { get { return "MerchantAPI.APIGateway.Test.Functional"; } }
    public override string DbConnectionString { get { return Configuration["ConnectionStrings:DBConnectionString"]; } }
    public string DbConnectionStringDDL { get { return Configuration["ConnectionStrings:DBConnectionStringDDL"]; } }


    public override void CheckWasCreatedFrom(FaultTriggerViewModelCreate post, FaultTriggerViewModelGet get)
    {
      Assert.AreEqual(post.Id, get.Id);
      Assert.AreEqual(post.Type, get.Type);
      Assert.AreEqual(post.Name, get.Name);
      Assert.AreEqual(post.SimulateSendTxsResponse, get.SimulateSendTxsResponse);
      Assert.AreEqual(post.DbFaultComponent, get.DbFaultComponent);
      Assert.AreEqual(post.DbFaultMethod, get.DbFaultMethod);
      Assert.AreEqual(post.FaultDelayMs, get.FaultDelayMs);
      Assert.AreEqual(post.FaultProbability, get.FaultProbability);
    }

    public override TestServer CreateServer(bool mockedServices, TestServer serverCallback, string dbConnectionString, IEnumerable<KeyValuePair<string, string>> overridenSettings = null)
    {
      return new TestServerBase(DbConnectionStringDDL).CreateServer<MapiServer, APIGatewayTestsMockStartup, APIGatewayTestsStartup>(mockedServices, serverCallback, dbConnectionString, overridenSettings);
    }

    public override string GetNonExistentKey() => "-1";
    public override string GetBaseUrl() => MapiServer.TestFaultUrl;
    public override string ExtractGetKey(FaultTriggerViewModelGet entry) => entry.Id.ToString();
    public override string ExtractPostKey(FaultTriggerViewModelCreate entry) => entry.Id.ToString();

    public override FaultTriggerViewModelCreate[] GetItemsToCreate()
    {
      return new[] {
        new FaultTriggerViewModelCreate()
        {
          Id = "1",
          Type = Faults.FaultType.DbBeforeSavingUncommittedState.ToString(),
          DbFaultComponent = Faults.DbFaultComponent.MapiAfterSendToNode.ToString(),
          FaultProbability = 10,
          DbFaultMethod = Faults.DbFaultMethod.Exception.ToString()
        },
        new FaultTriggerViewModelCreate()
        {
          Id = "2",
          Type = Faults.FaultType.DbBeforeSavingUncommittedState.ToString(),
          DbFaultComponent = Faults.DbFaultComponent.MapiBeforeSendToNode.ToString(),
          FaultProbability = 20,
          DbFaultMethod = Faults.DbFaultMethod.Exception.ToString()
        }
      };
    }

    public override FaultTriggerViewModelCreate GetItemToCreate()
    {
      return new FaultTriggerViewModelCreate()
      {
        Id = "1a",
        Type = Faults.FaultType.DbBeforeSavingUncommittedState.ToString(),
        DbFaultComponent = Faults.DbFaultComponent.MapiAfterSendToNode.ToString(),
        FaultProbability = 100,
        DbFaultMethod = Faults.DbFaultMethod.Exception.ToString()
      };
    }

    public override void ModifyEntry(FaultTriggerViewModelCreate entry)
    {
      entry.Type = Faults.FaultType.SimulateSendTxsMapi.ToString();
      entry.SimulateSendTxsResponse = Faults.SimulateSendTxsResponse.NodeFailsWhenSendRawTxs.ToString();
      entry.DbFaultComponent = null;
      entry.FaultProbability = 90;
    }

    public override void SetPostKey(FaultTriggerViewModelCreate entry, string key)
    {
      entry.Id = key;
    }

    [TestInitialize]
    public void TestInitialize()
    {
      Initialize(mockedServices: false);
      ApiKeyAuthentication = AppSettings.RestAdminAPIKey;
    }

    [TestCleanup]
    public void TestCleanup()
    {
      Cleanup();
    }

    [TestMethod]
    public async Task TestPost_WithInvalidAuthentication()
    {
      ApiKeyAuthentication = null;
      RestAuthentication = MockedIdentityBearerAuthentication;
      var entryPost = GetItemToCreate();
      var (_, _) = await Post<FaultTriggerViewModelCreate, FaultTriggerViewModelGet>(Client, entryPost, HttpStatusCode.Unauthorized);

      ApiKeyAuthentication = AppSettings.RestAdminAPIKey;
      RestAuthentication = null;
      (_, _) = await Post<FaultTriggerViewModelCreate, FaultTriggerViewModelGet>(Client, entryPost, HttpStatusCode.Created);
    }

    [TestMethod]
    public async Task InvalidPost()
    {
      // test invalid fault trigger 
      var entryPost = GetItemToCreate();
      entryPost.SimulateSendTxsResponse = Faults.SimulateSendTxsResponse.NodeFailsAfterSendRawTxs.ToString();
      await Post<FaultTriggerViewModelCreate, FaultTriggerViewModelGet>(Client, entryPost, HttpStatusCode.BadRequest);

      entryPost = GetItemToCreate();
      entryPost.DbFaultComponent = null;
      await Post<FaultTriggerViewModelCreate, FaultTriggerViewModelGet>(Client, entryPost, HttpStatusCode.BadRequest);

      // test invalid FaultProbability
      entryPost = GetItemToCreate();
      entryPost.FaultProbability = 101;
      await Post<FaultTriggerViewModelCreate, FaultTriggerViewModelGet>(Client, entryPost, HttpStatusCode.BadRequest);

      entryPost = GetItemToCreate();
      entryPost.FaultProbability = -1;
      await Post<FaultTriggerViewModelCreate, FaultTriggerViewModelGet>(Client, entryPost, HttpStatusCode.BadRequest);
    }
  }
}
