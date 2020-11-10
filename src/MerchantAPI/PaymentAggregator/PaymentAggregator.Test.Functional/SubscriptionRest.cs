// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.Common.Test;
using MerchantAPI.PaymentAggregator.Domain;
using MerchantAPI.PaymentAggregator.Domain.Repositories;
using MerchantAPI.PaymentAggregator.Infrastructure.Repositories;
using MerchantAPI.PaymentAggregator.Rest.ViewModels;
using MerchantAPI.PaymentAggregator.Test.Functional.Server;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MerchantAPI.PaymentAggregator.Test.Functional
{
  [TestClass]
  public class SubscriptionRest : CommonRestMethodsBase<SubscriptionViewModelGet, SubscriptionViewModelCreate, AppSettings>
  {
    public override string LOG_CATEGORY { get { return "MerchantAPI.PaymentAggregator.Test.Functional"; } }
    public override string DbConnectionString { get { return Configuration["PaymentAggregatorConnectionStrings:DBConnectionString"]; } }

    public override TestServer CreateServer(bool mockedServices, TestServer serverCallback, string dbConnectionString)
    {
      return new TestServerBase().CreateServer<MapiServer, PaymentAggregatorTestsStartup, MerchantAPI.PaymentAggregator.Rest.Startup>(mockedServices, serverCallback, dbConnectionString);
    }

    private void CreateAccount()
    {
      var create = new AccountViewModelCreate
      {
        ContactFirstName = "Name1",
        ContactLastName = "Name2",
        Email = "test@crea.si",
        Identity = "5",
        OrganisationName = "Organisation",
        IdentityProvider = "http://mysite.com"
      };
      ApiKeyAuthentication = AppSettings.RestAdminAPIKey;

      var content = new StringContent(JsonSerializer.Serialize(create), Encoding.UTF8, "application/json");
      _ = Post<AccountViewModelGet>(MapiServer.ApiAccountUrl, client, content, HttpStatusCode.Created).Result;
      ApiKeyAuthentication = null;
    }

    [TestInitialize]
    public void TestInitialize()
    {
      Initialize(mockedServices: true);
      CreateAccount();
      RestAuthentication = "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiI1IiwibmJmIjoxNTk5NDExNDQzLCJleHAiOjE5MTQ3NzE0NDMsImlhdCI6MTU5OTQxMTQ0MywiaXNzIjoiaHR0cDovL215c2l0ZS5jb20iLCJhdWQiOiJodHRwOi8vbXlhdWRpZW5jZS5jb20ifQ.Z43NASAbIxMZrL2MzbJTJD30hYCxhoAs-8heDjQMnjM";
    }

    [TestCleanup]
    public void TestCleanup()
    {
      Cleanup();
    }

    public override void CheckWasCreatedFrom(SubscriptionViewModelCreate post, SubscriptionViewModelGet get)
    {
      Assert.AreEqual(post.Id, get.SubscriptionId);
      Assert.AreEqual(post.ServiceType, get.ServiceType);
    }

    public override string ExtractGetKey(SubscriptionViewModelGet entry) => entry.SubscriptionId.ToString();

    public override string ExtractPostKey(SubscriptionViewModelCreate entry) => entry.Id.ToString();

    public override string GetBaseUrl() => MapiServer.ApiSubscriptionUrl;

    public override string GetNonExistentKey() => int.MaxValue.ToString();

    public override SubscriptionViewModelCreate[] GetItemsToCreate()
    {
      return new[]
      {
        new SubscriptionViewModelCreate
        {
          Id = 1,
          ServiceType = Consts.ServiceType.allFeeQuotes
        },
        new SubscriptionViewModelCreate
        {
          Id = 2,
          ServiceType = Consts.ServiceType.queryTx
        }
      };
    }

    public override SubscriptionViewModelCreate GetItemToCreate()
    {
      return new SubscriptionViewModelCreate
              {
                Id = 1,
                ServiceType = Consts.ServiceType.allFeeQuotes
              };
    }

    public override void ModifyEntry(SubscriptionViewModelCreate entry)
    {
      entry.ServiceType += "Modified";
    }

    public override void SetPostKey(SubscriptionViewModelCreate entry, string key)
    {
      return;
    }

    [Ignore]
    public override Task Put()
    {
      return base.Put();
    }


    [TestMethod]
    public override async Task DeleteTest()
    {
      var entries = GetItemsToCreate();

      foreach (var entry in entries)
      {
        // Create new one using POST
        await Post<SubscriptionViewModelCreate, SubscriptionViewModelGet>(client, entry, HttpStatusCode.Created);
      }

      // Check if all are there
      foreach (var entry in entries)
      {
        // Create new one using POST
        await Get<SubscriptionViewModelGet>(client, UrlForKey(ExtractPostKey(entry)), HttpStatusCode.OK);
      }

      var firstKey = ExtractPostKey(entries.First());

      // Delete first one
      await Delete(client, UrlForKey(firstKey));

      // GET should find the first item with validTo field set, but it should not find the rest with validTo set
      foreach (var entry in entries)
      {
        var key = ExtractPostKey(entry);
        var item = await Get<SubscriptionViewModelGet>(client, UrlForKey(key), HttpStatusCode.OK);
        if (key == firstKey)
        {
          Assert.IsNotNull(item.ValidTo);
        }
        else
        {
          Assert.IsNull(item.ValidTo);
        }
      }
    }

    [TestMethod]
    public async Task CheckValidServiceType()
    {
      var invalidServiceType = new SubscriptionViewModelCreate
      {
        ServiceType = "InvalidServiceType"
      };

      await Post<SubscriptionViewModelCreate, SubscriptionViewModelGet>(client, invalidServiceType, HttpStatusCode.BadRequest);

      var validServiceType = GetItemToCreate();

      await Post<SubscriptionViewModelCreate, SubscriptionViewModelGet>(client, validServiceType, HttpStatusCode.Created);
    }

    [TestMethod]
    public async Task CheckActiveSubscription()
    {
      var entries = GetItemsToCreate();

      Assert.AreEqual(2, entries.Count());

      foreach (var entry in entries)
      {
        // Create new one using POST
        await Post<SubscriptionViewModelCreate, SubscriptionViewModelGet>(client, entry, HttpStatusCode.Created);
      }

      var returnedItems = await Get<SubscriptionViewModelGet[]>(client, UrlForKey(null), HttpStatusCode.OK);

      Assert.AreEqual(2, returnedItems.Count());

      await Delete(client, UrlForKey(ExtractPostKey(entries.First())));

      returnedItems = await Get<SubscriptionViewModelGet[]>(client, UrlForKey(null), HttpStatusCode.OK);

      Assert.AreEqual(1, returnedItems.Count());
      Assert.AreEqual(entries.Last().ServiceType, returnedItems.Single().ServiceType);

      returnedItems = await Get<SubscriptionViewModelGet[]>(client, GetBaseUrl() + "?onlyActive=false", HttpStatusCode.OK);
      Assert.AreEqual(2, returnedItems.Count());
    }

    [TestMethod]
    public async Task Deleting2ndTimeShouldNotChangeValidTo()
    {
      var item = GetItemToCreate();

      await Post<SubscriptionViewModelCreate, SubscriptionViewModelGet>(client, item, HttpStatusCode.Created);
      
      var returnedItem = await Get<SubscriptionViewModelGet>(client, UrlForKey(ExtractPostKey(item)), HttpStatusCode.OK);
      Assert.IsNull(returnedItem.ValidTo);

      await Delete(client, UrlForKey(ExtractPostKey(item)));

      returnedItem = await Get<SubscriptionViewModelGet>(client, UrlForKey(ExtractPostKey(item)), HttpStatusCode.OK);
      Assert.IsTrue(returnedItem.ValidTo.HasValue);
      var validTo = returnedItem.ValidTo.Value;

      await Delete(client, UrlForKey(ExtractPostKey(item)));

      returnedItem = await Get<SubscriptionViewModelGet>(client, UrlForKey(ExtractPostKey(item)), HttpStatusCode.OK);

      Assert.IsTrue(returnedItem.ValidTo.HasValue);
      Assert.AreEqual(validTo, returnedItem.ValidTo);
    }
  }
}
