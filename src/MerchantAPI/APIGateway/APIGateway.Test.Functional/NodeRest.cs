// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain;
using MerchantAPI.APIGateway.Rest.ViewModels;
using MerchantAPI.APIGateway.Test.Functional.Mock;
using MerchantAPI.APIGateway.Test.Functional.Server;
using MerchantAPI.Common.BitcoinRpc;
using MerchantAPI.Common.Json;
using MerchantAPI.Common.Test;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Test.Functional
{
  [TestCategory("TestCategoryNo1")]
  [TestClass]
  public class NodeRest : CommonRestMethodsBase<NodeViewModelGet, NodeViewModelCreate, AppSettings> 
  {
    public override string LOG_CATEGORY { get { return "MerchantAPI.APIGateway.Test.Functional"; } }
    public override string DbConnectionString { get { return Configuration["ConnectionStrings:DBConnectionString"]; } }
    public string DbConnectionStringDDL { get { return Configuration["ConnectionStrings:DBConnectionStringDDL"]; } }

    public override TestServer CreateServer(bool mockedServices, TestServer serverCallback, string dbConnectionString, IEnumerable<KeyValuePair<string, string>> overridenSettings = null)
    {
      return new TestServerBase(DbConnectionStringDDL).CreateServer<MapiServer, APIGatewayTestsMockWithDBInsertStartup, APIGatewayTestsStartup>(mockedServices, serverCallback, dbConnectionString, overridenSettings);
    }

    protected RpcClientFactoryMock rpcClientFactoryMock;

    [TestInitialize]
    public void TestInitialize()
    {
      Initialize(mockedServices: true);
      ApiKeyAuthentication = AppSettings.RestAdminAPIKey;

      rpcClientFactoryMock = server.Services.GetRequiredService<IRpcClientFactory>() as RpcClientFactoryMock;

      if (rpcClientFactoryMock != null)
      {
        rpcClientFactoryMock.AddKnownBlock(0, HelperTools.HexStringToByteArray(TestBase.genesisBlock));

        rpcClientFactoryMock.Reset(); // remove calls that are used to test node connection when adding a new node
      }
    }

    [TestCleanup]
    public void TestCleanup()
    {
      Cleanup();
    }


    public override string GetNonExistentKey() => "ThisKeyDoesNotExists:123";
    public override string GetBaseUrl() => MapiServer.ApiNodeUrl;
    public override string ExtractGetKey(NodeViewModelGet entry) => entry.Id;
    public override string ExtractPostKey(NodeViewModelCreate entry) => entry.Id;

    public override void SetPostKey(NodeViewModelCreate entry, string key)
    {
      entry.Id = key;
    }

    public override NodeViewModelCreate GetItemToCreate()
    {
      return new NodeViewModelCreate
      {
        Id = "some.host:123",
        Remarks = "Some remarks",
        Password = "somePassword",
        Username = "someUsername"
      };
    }

    public override void ModifyEntry(NodeViewModelCreate entry)
    {
      _ = int.TryParse(entry.Remarks[^1..], out int result);
      entry.Remarks = $"Updated remarks { result }";
      entry.Username = $"updatedUsername { result }";
      entry.ZMQNotificationsEndpoint = $"tcp://updatedEndpoint:123{ result }";
    }

    public override NodeViewModelCreate[] GetItemsToCreate()
    {
      return
        new[]
        {
          new NodeViewModelCreate
          {
            Id = "some.host1:123",
            Remarks = "Some remarks1",
            Password = "somePassword1",
            Username = "user1"
          },

          new NodeViewModelCreate
          {
            Id = "some.host2:123",
            Remarks = "Some remarks2",
            Password = "somePassword2",
            Username = "user2"
          },
        };

    }

    public override void CheckWasCreatedFrom(NodeViewModelCreate post, NodeViewModelGet get)
    {
      Assert.AreEqual(post.Id.ToLower(), get.Id.ToLower()); // Ignore key case
      Assert.AreEqual(post.Remarks, get.Remarks);
      Assert.AreEqual(post.Username, get.Username);
      Assert.AreEqual(post.ZMQNotificationsEndpoint, get.ZMQNotificationsEndpoint);
      // Password can not be retrieved. We also do not check additional fields such as LastErrorAt
    }


    [TestMethod]
    public async Task CreateNode_WrongIdSyntax_ShouldReturnBadRequest()
    {
      //arrange
      var create = new NodeViewModelCreate
      {
        Id = "some.host2", // missing port
        Remarks = "Some remarks2",
        Password = "somePassword2",
        Username = "user2"
      };
      var content = new StringContent(JsonSerializer.Serialize(create), Encoding.UTF8, "application/json");

      //act
      var (_, responseContent) = await Post<string>(UrlForKey(""), Client, content, HttpStatusCode.BadRequest);
      var responseAsString = await responseContent.Content.ReadAsStringAsync();

      var vpd = JsonSerializer.Deserialize<ValidationProblemDetails>(responseAsString);

      Assert.AreEqual(1, vpd.Errors.Count);
      Assert.AreEqual("Id", vpd.Errors.First().Key);
    }

    [TestMethod]
    public async Task CreateNode_WrongIdSyntax2_ShouldReturnBadRequest()
    {
      //arrange
      var create = new NodeViewModelCreate
      {
        Id = "some.host2:abs", // not a port number
        Remarks = "Some remarks2",
        Password = "somePassword2",
        Username = "user2"
      };
      var content = new StringContent(JsonSerializer.Serialize(create), Encoding.UTF8, "application/json");

      //act
      var (_, responseContent) = await Post<string>(UrlForKey(""), Client, content, HttpStatusCode.BadRequest);
      var responseAsString = await responseContent.Content.ReadAsStringAsync();

      var vpd = JsonSerializer.Deserialize<ValidationProblemDetails>(responseAsString);
      Assert.AreEqual(1, vpd.Errors.Count);
      Assert.AreEqual("Id", vpd.Errors.First().Key);
    }

    [TestMethod]
    public async Task CreateNode_NoUsername_ShouldReturnBadRequest()
    {
      //arrange
      var create = new NodeViewModelCreate
      {
        Id = "some.host2:2",
        Remarks = "Some remarks2",
        Password = "somePassword2",
        Username = null // missing username
      };
      var content = new StringContent(JsonSerializer.Serialize(create), Encoding.UTF8, "application/json");

      //act
      var (_, responseContent) = await Post<string>(UrlForKey(""), Client, content, HttpStatusCode.BadRequest);

      var responseAsString =await responseContent.Content.ReadAsStringAsync();
      var vpd = JsonSerializer.Deserialize<ValidationProblemDetails>(responseAsString);
      Assert.AreEqual(1, vpd.Errors.Count);
      Assert.AreEqual("Username", vpd.Errors.First().Key);
    }

    [TestMethod]
    public async Task CreateNode_DuplicateZMQNotificationsEndpoint_ShouldReturnBadRequest()
    {
      //arrange
      var node1 = new NodeViewModelCreate
      {
        Id = "some.host1:123",
        Remarks = "Some remarks1",
        Password = "somePassword1",
        Username = "user1",
        ZMQNotificationsEndpoint = "tcp://0.0.0.0:123"
      };

      var node2 = new NodeViewModelCreate
      {
        Id = "some.host2:123",
        Remarks = "Some remarks 2",
        Password = "somePassword2",
        Username = "user2",
        ZMQNotificationsEndpoint = "tcp://0.0.0.0:123"
      };

      var content = new StringContent(JsonSerializer.Serialize(node1), Encoding.UTF8, "application/json");

      //act
      await Post<NodeViewModelGet>(UrlForKey(""), Client, content, HttpStatusCode.Created);

      var content2 = new StringContent(JsonSerializer.Serialize(node2), Encoding.UTF8, "application/json");

      //act
      await Post<string>(UrlForKey(""), Client, content2, HttpStatusCode.BadRequest);
    }
    
    [TestMethod]
    public async Task UpdateNode_NoUsername_ShouldReturnBadRequest()
    {
      //arrange
      var create = new NodeViewModelPut
      {
        Remarks = "Some remarks2",
        Password = "somePassword2",
        Username = null // missing username
      };

      //act
      await Put(Client, UrlForKey("some.host2:2"), create, HttpStatusCode.BadRequest);

    }

    [TestMethod]
    public async Task UpdateNode_DuplicateZMQNotificationsEndpoint_ShouldReturnBadRequest()
    {
      //arrange
      var node1 = new NodeViewModelCreate
      {
        Id = "some.host1:123",
        Remarks = "Some remarks1",
        Password = "somePassword1",
        Username = "user1",
        ZMQNotificationsEndpoint = "tcp://0.0.0.0:123"
      };

      var node2 = new NodeViewModelCreate
      {
        Id = "some.host2:123",
        Remarks = "Some remarks 2",
        Password = "somePassword2",
        Username = "user2",
        ZMQNotificationsEndpoint = "tcp://0.0.0.0:1234"
      };

      var content = new StringContent(JsonSerializer.Serialize(node1), Encoding.UTF8, "application/json");

      //act
      await Post<NodeViewModelGet>(UrlForKey(""), Client, content, HttpStatusCode.Created);

      var content2 = new StringContent(JsonSerializer.Serialize(node2), Encoding.UTF8, "application/json");

      //act
      await Post<NodeViewModelGet>(UrlForKey(""), Client, content2, HttpStatusCode.Created);

      //create PUT request with invalid (existing) ZMQNotificationsEndpoint
      var putNode1 = new NodeViewModelPut
      {
        Remarks = "Some remarks12",
        Password = "somePassword12",
        Username = "user12",
        ZMQNotificationsEndpoint = "tcp://0.0.0.0:1234"
      };

      //act
      await Put(Client, UrlForKey("some.host1:123"), putNode1, HttpStatusCode.BadRequest);

      await Put(Client, UrlForKey("SOME.HOST1:123"), putNode1, HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task CreateNode_InvalidZMQNotificationsEndpoint()
    {
      //arrange
      var create = new NodeViewModelCreate
      {
        Id = "some.host2:2",
        Remarks = "Some remarks2",
        Password = "somePassword2",
        Username = "someUsername",
        ZMQNotificationsEndpoint = "invalidEndpoint"
      };
      var content = new StringContent(JsonSerializer.Serialize(create), Encoding.UTF8, "application/json");

      //act
      var(_, responseContent) = await Post<string>(UrlForKey(""), Client, content, HttpStatusCode.BadRequest);

      var responseAsString = await responseContent.Content.ReadAsStringAsync();
      var vpd = JsonSerializer.Deserialize<ValidationProblemDetails>(responseAsString);
      Assert.AreEqual(1, vpd.Errors.Count);
      Assert.AreEqual("ZMQNotificationsEndpoint", vpd.Errors.First().Key);
    }

    [TestMethod]
    public async Task UpdateNode_InvalidZMQNotificationsEndpoint()
    {
     //arrange
     var entryPost = GetItemToCreate();
     var entryPostKey = ExtractPostKey(entryPost);

     //before we can update it, we have to POST node first
     await Post<NodeViewModelCreate, NodeViewModelGet>(Client, entryPost, HttpStatusCode.Created);

     var entryPut = GetItemToCreate();
     entryPut.ZMQNotificationsEndpoint = "tcp://1.2.3.4:invalid";

     //invalid port - should return badRequest
     await Put(Client, UrlForKey(entryPostKey), entryPut, HttpStatusCode.BadRequest);

     entryPut = GetItemToCreate();
     entryPut.ZMQNotificationsEndpoint = "tcp://1.2.3.4:28333";

     //should succeed
     await Put(Client, UrlForKey(entryPostKey), entryPut, HttpStatusCode.NoContent);

     entryPut.ZMQNotificationsEndpoint = "";
     //empty string should succeed
     await Put(Client, UrlForKey(entryPostKey), entryPut, HttpStatusCode.NoContent);
    }
  }
}
