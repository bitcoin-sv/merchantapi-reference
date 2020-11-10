// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.PaymentAggregator.Rest.ViewModels;
using MerchantAPI.PaymentAggregator.Test.Functional.Server;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using MerchantAPI.Common.Test;
using MerchantAPI.PaymentAggregator.Domain;
using Microsoft.AspNetCore.TestHost;
using MerchantAPI.PaymentAggregator.Infrastructure.Repositories;
using MerchantAPI.PaymentAggregator.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace MerchantAPI.PaymentAggregator.Test.Functional
{
  [TestClass]
  public class AccountRest : CommonRestMethodsBase<AccountViewModelGet, AccountViewModelCreate, AppSettings>
  {
    public override string LOG_CATEGORY { get { return "MerchantAPI.PaymentAggregator.Test.Functional"; } }
    public override string DbConnectionString { get { return Configuration["PaymentAggregatorConnectionStrings:DBConnectionString"]; } }

    public override TestServer CreateServer(bool mockedServices, TestServer serverCallback, string dbConnectionString)
    {
      return new TestServerBase().CreateServer<MapiServer, PaymentAggregatorTestsStartup, MerchantAPI.PaymentAggregator.Rest.Startup>(mockedServices, serverCallback, dbConnectionString);
    }

    [TestInitialize]
    public void TestInitialize()
    {
      Initialize(mockedServices: true);
      ApiKeyAuthentication = AppSettings.RestAdminAPIKey;
    }

    [TestCleanup]
    public void TestCleanup()
    {
      Cleanup();
    }

    public override string GetNonExistentKey() => int.MaxValue.ToString();


    public override void CheckWasCreatedFrom(AccountViewModelCreate post, AccountViewModelGet get)
    {
      Assert.AreEqual(post.ContactFirstName, get.ContactFirstName);
      Assert.AreEqual(post.ContactLastName, get.ContactLastName);
      Assert.AreEqual(post.Email, get.Email);
      Assert.AreEqual(post.Identity, get.Identity);
      Assert.AreEqual(post.OrganisationName, get.OrganisationName);
      Assert.AreEqual(post.IdentityProvider, get.IdentityProvider);
    }

    public override string ExtractGetKey(AccountViewModelGet entry) => entry.AccountId.ToString();

    public override string ExtractPostKey(AccountViewModelCreate entry) => entry.Id.ToString();

    public override string GetBaseUrl() => MapiServer.ApiAccountUrl;

    public override AccountViewModelCreate[] GetItemsToCreate()
    {
      return new[]
      {
        new AccountViewModelCreate
        {
          Id = 1,
          ContactFirstName = "ContactFirstName1",
          ContactLastName = "ContactLastName1",
          Email = "test@crea.si",
          Identity = "Identity1",
          OrganisationName = "OrganisationName1",
          IdentityProvider = "IdentityProvider1"
        },
        new AccountViewModelCreate
        {
          Id = 2,
          ContactFirstName = "ContactFirstName2",
          ContactLastName = "ContactLastName2",
          Email = "test@crea.si",
          Identity = "Identity2",
          OrganisationName = "OrganisationName2",
          IdentityProvider = "IdentityProvider2"
        }
      };
    }

    public override AccountViewModelCreate GetItemToCreate()
    {
      return new AccountViewModelCreate
      {
        Id = 1,
        ContactFirstName = "ContactFirstName1",
        ContactLastName = "ContactLastName1",
        Email = "test@crea.si",
        Identity = "Identity1",
        OrganisationName = "OrganisationName1",
        IdentityProvider = "IdentityProvider1"
      };
    }

    public override void ModifyEntry(AccountViewModelCreate entry)
    {
      entry.ContactFirstName = "updated ContactFirstName1";
      entry.ContactLastName = "updated ContactLastName1";
      entry.Email = "test2@crea.si";
      entry.Identity = "updated Identity1";
      entry.OrganisationName = "updated OrganisationName1";
      entry.IdentityProvider = "updated IdentityProvider1";
    }

    public override void SetPostKey(AccountViewModelCreate entry, string key)
    {
      return;
    }

    [Ignore]
    public override Task DeleteTest()
    {
      return base.DeleteTest();
    }

    [Ignore]
    public override Task Delete_NoElement_ShouldReturnNoContent()
    {
      return base.Delete_NoElement_ShouldReturnNoContent();
    }

    [TestMethod]
    public async Task CreateAccountWithSameIdentityShouldReturnErrorAsync()
    {
      var create = new AccountViewModelCreate
      {
        ContactFirstName = "Name1",
        ContactLastName = "Name2",
        Email = "test@crea.si",
        Identity = "Identity",
        OrganisationName = "Organisation",
        IdentityProvider = "IdentityProvider1"
      };

      var content = new StringContent(JsonSerializer.Serialize(create), Encoding.UTF8, "application/json");
      _ = await Post<AccountViewModelGet>(UrlForKey(""), client, content, HttpStatusCode.Created);

      content = new StringContent(JsonSerializer.Serialize(create), Encoding.UTF8, "application/json");
      _ = await Post<AccountViewModelGet>(UrlForKey(""), client, content, HttpStatusCode.Conflict);
    }

    [TestMethod]
    public async Task CreateAccountWithMissingRequiredFieldsShouldReturnErrorAsync()
    {
      var create = new AccountViewModelCreate
      {
        ContactFirstName = "Name1",
        ContactLastName = "Name2",
      };

      var content = new StringContent(JsonSerializer.Serialize(create), Encoding.UTF8, "application/json");
      var (_, responseContent) = await Post<AccountViewModelGet>(UrlForKey(""), client, content, HttpStatusCode.BadRequest);
      var responseAsString = await responseContent.Content.ReadAsStringAsync();

      var vpd = JsonSerializer.Deserialize<ValidationProblemDetails>(responseAsString);
      Assert.AreEqual(4, vpd.Errors.Count());
      Assert.IsTrue(vpd.Errors.Any(x => x.Key == "Identity"));
      Assert.IsTrue(vpd.Errors.Any(x => x.Key == "OrganisationName"));
      Assert.IsTrue(vpd.Errors.Any(x => x.Key == "Email"));
      Assert.IsTrue(vpd.Errors.Any(x => x.Key == "IdentityProvider"));
    }
  }
}
