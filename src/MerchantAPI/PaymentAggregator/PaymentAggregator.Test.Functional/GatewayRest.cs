// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.Common.Clock;
using MerchantAPI.Common.EventBus;
using MerchantAPI.Common.Test;
using MerchantAPI.PaymentAggregator.Domain;
using MerchantAPI.PaymentAggregator.Domain.Models;
using MerchantAPI.PaymentAggregator.Domain.Repositories;
using MerchantAPI.PaymentAggregator.Infrastructure.Repositories;
using MerchantAPI.PaymentAggregator.Rest.ViewModels;
using MerchantAPI.PaymentAggregator.Test.Functional.Server;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace MerchantAPI.PaymentAggregator.Test.Functional
{
  [TestClass]
  public class GatewayRest : CommonRestMethodsBase<GatewayViewModelGet, GatewayViewModelCreate, AppSettings>
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


    public override string GetNonExistentKey() => "-1";
    public override string GetBaseUrl() => MapiServer.ApiGatewayUrl;
    public override string ExtractGetKey(GatewayViewModelGet entry) => entry.Id.ToString();
    public override string ExtractPostKey(GatewayViewModelCreate entry) => entry.Id.ToString();

    public override void SetPostKey(GatewayViewModelCreate entry, string key)
    {
      entry.Id = int.Parse(key);
    }

    public override GatewayViewModelCreate GetItemToCreate()
    {
      return new GatewayViewModelCreate
      {
        Id = 1,
        Url = "http://host:1234/",
        MinerRef = "someMinerRef",
        Email = "some@Email.com",
        OrganisationName = "someOrganisation",
        ContactFirstName = "someContactFirstName",
        ContactLastName = "someContactLastName",
        Remarks = "Some remarks"
    };
    }

    public override void ModifyEntry(GatewayViewModelCreate entry)
    {
      entry.Url += "/updated/";
      entry.MinerRef = "updatedMinerRef";
      entry.Email = "updated@Email.com";
      entry.OrganisationName = "updatedOrganisation";
      entry.ContactFirstName = "updatedContactFirstName";
      entry.ContactLastName = "updatedContactLastName";
      entry.Remarks += "Updated remarks";
    }

    public override GatewayViewModelCreate[] GetItemsToCreate()
    {
      return
        new[]
        {
          new GatewayViewModelCreate
          {
            Id = 1,
            Url = "http://host1:1234/",
            MinerRef ="someMinerRef1",
            Email = "some@Email1.com",
            OrganisationName = "someOrganisation1",
            ContactFirstName = "someContactFirstName1",
            ContactLastName = "someContactLastName1",
            Remarks = "Some remarks1"
          },

          new GatewayViewModelCreate
          {
            Id = 2,
            Url = "http://host2:1234/",
            MinerRef ="someMinerRef2",
            Email = "some@Email2.com",
            OrganisationName = "someOrganisation2",
            ContactFirstName = "someContactFirstName2",
            ContactLastName = "someContactLastName2",
            Remarks = "Some remarks2"
          },
        };
    }

    public override void CheckWasCreatedFrom(GatewayViewModelCreate post, GatewayViewModelGet get)
    {
      Assert.AreEqual(post.Id, get.Id);
      Assert.AreEqual(post.Url.ToLower(), get.Url.ToLower()); // Ignore key case
      Assert.AreEqual(post.MinerRef, get.MinerRef);
      Assert.AreEqual(post.Email, get.Email);
      Assert.AreEqual(post.OrganisationName, get.OrganisationName);
      Assert.AreEqual(post.ContactFirstName, get.ContactFirstName);
      Assert.AreEqual(post.ContactLastName, get.ContactLastName);
      Assert.AreEqual(post.Remarks, get.Remarks);

      // We do not check additional fields such as LastErrorAt
    }

    public string UrlForGetMultiple(bool onlyActive = false)
    {
      string url = GetBaseUrl(); 
      if (onlyActive)
      {
        url += $"?onlyActive=true";
      }
      return url;
    }

    [TestMethod]
    public async Task InvalidPost()
    {
      // Try to create it - it should fail
      var entryPost = GetItemToCreate();
      entryPost.ContactFirstName = null;
      await Post<GatewayViewModelCreate, GatewayViewModelGet>(client, entryPost, HttpStatusCode.BadRequest);
      entryPost = GetItemToCreate();
      entryPost.ContactFirstName = "  ";
      await Post<GatewayViewModelCreate, GatewayViewModelGet>(client, entryPost, HttpStatusCode.BadRequest);

      entryPost = GetItemToCreate();
      entryPost.ContactLastName = null;
      await Post<GatewayViewModelCreate, GatewayViewModelGet>(client, entryPost, HttpStatusCode.BadRequest);
      entryPost = GetItemToCreate();
      entryPost.ContactLastName = "  ";
      await Post<GatewayViewModelCreate, GatewayViewModelGet>(client, entryPost, HttpStatusCode.BadRequest);

      entryPost = GetItemToCreate();
      entryPost.Email = null;
      await Post<GatewayViewModelCreate, GatewayViewModelGet>(client, entryPost, HttpStatusCode.BadRequest);
      entryPost = GetItemToCreate();
      entryPost.Email = "  ";
      await Post<GatewayViewModelCreate, GatewayViewModelGet>(client, entryPost, HttpStatusCode.BadRequest);

      entryPost = GetItemToCreate();
      entryPost.MinerRef = null;
      await Post<GatewayViewModelCreate, GatewayViewModelGet>(client, entryPost, HttpStatusCode.BadRequest);
      entryPost = GetItemToCreate();
      entryPost.MinerRef = "  ";
      await Post<GatewayViewModelCreate, GatewayViewModelGet>(client, entryPost, HttpStatusCode.BadRequest);

      entryPost = GetItemToCreate();
      entryPost.OrganisationName = null;
      await Post<GatewayViewModelCreate, GatewayViewModelGet>(client, entryPost, HttpStatusCode.BadRequest);
      entryPost = GetItemToCreate();
      entryPost.OrganisationName = "  ";
      await Post<GatewayViewModelCreate, GatewayViewModelGet>(client, entryPost, HttpStatusCode.BadRequest);

      entryPost = GetItemToCreate();
      entryPost.Url = null;
      await Post<GatewayViewModelCreate, GatewayViewModelGet>(client, entryPost, HttpStatusCode.BadRequest);
      entryPost = GetItemToCreate();
      entryPost.Url = "  ";
      await Post<GatewayViewModelCreate, GatewayViewModelGet>(client, entryPost, HttpStatusCode.BadRequest);
      entryPost = GetItemToCreate();
      entryPost.Url = "host:port";
      await Post<GatewayViewModelCreate, GatewayViewModelGet>(client, entryPost, HttpStatusCode.BadRequest);

      // only successful call
      entryPost = GetItemToCreate();
      await Post<GatewayViewModelCreate, GatewayViewModelGet>(client, entryPost, HttpStatusCode.Created);
      // check GET all
      var getEntries = await Get<GatewayViewModelGet[]>(client, GetBaseUrl(), HttpStatusCode.OK);
      Assert.AreEqual(1, getEntries.Count());
    }

    [TestMethod]
    public async Task TestActionsWithDisabledGateway()
    {
      // arrange
      var entryPostWithDisabled = GetItemToCreate();
      entryPostWithDisabled.DisabledAt = DateTime.UtcNow;
      var entryPostKeyDisabled = ExtractPostKey(entryPostWithDisabled);

      // check POST
      await Post<GatewayViewModelCreate, GatewayViewModelGet>(client, entryPostWithDisabled, HttpStatusCode.Created);
      await Post<GatewayViewModelCreate, GatewayViewModelGet>(client, entryPostWithDisabled, HttpStatusCode.Conflict);

      // check GET
      var entryGot = await Get<GatewayViewModelGet>(client, UrlForKey(entryPostKeyDisabled), HttpStatusCode.OK);
      CheckWasCreatedFrom(entryPostWithDisabled, entryGot);
      // check GET all
      var getEntries = await Get<GatewayViewModelGet[]>(client, UrlForGetMultiple(), HttpStatusCode.OK);
      Assert.AreEqual(1, getEntries.Count());
      CheckWasCreatedFrom(entryPostWithDisabled, getEntries.Single());
      // check GET with onlyActive
      getEntries = await Get<GatewayViewModelGet[]>(client, UrlForGetMultiple(true), HttpStatusCode.OK);
      Assert.AreEqual(0, getEntries.Count());

      // check PUT
      ModifyEntry(entryPostWithDisabled);
      await Put(client, UrlForKey(entryPostKeyDisabled), entryPostWithDisabled, HttpStatusCode.NoContent);
      entryGot = await Get<GatewayViewModelGet>(client, UrlForKey(entryPostKeyDisabled), HttpStatusCode.OK);
      CheckWasCreatedFrom(entryPostWithDisabled, entryGot);

      // check DELETE
      await Delete(client, UrlForKey(entryPostKeyDisabled));
      await Get<GatewayViewModelGet>(client, UrlForKey(entryPostKeyDisabled), HttpStatusCode.NotFound);
    }

    [TestMethod]
    public async Task GetMultipleWithDifferentParameters()
    {
      // arrange
      var entryPost = GetItemsToCreate()[0];
      await Post<GatewayViewModelCreate, GatewayViewModelGet>(client, entryPost, HttpStatusCode.Created);
      var entryPostWithDisabled = GetItemsToCreate()[1];
      entryPostWithDisabled.Id = 2;
      entryPostWithDisabled.DisabledAt = DateTime.UtcNow.AddMinutes(1);
      var (entryResponsePostWithDisabled, _) = await Post<GatewayViewModelCreate, GatewayViewModelGet>(client, entryPostWithDisabled, HttpStatusCode.Created);
      
      // check GET with onlyActive
      var getEntries = await Get<GatewayViewModelGet[]>(client, UrlForGetMultiple(true), HttpStatusCode.OK);
      Assert.AreEqual(2, getEntries.Count());

      //act
      using (MockedClock.NowIs(entryResponsePostWithDisabled.DisabledAt.Value))
      {
        // check GET 
        getEntries = await Get<GatewayViewModelGet[]>(client, UrlForGetMultiple(), HttpStatusCode.OK);
        Assert.AreEqual(2, getEntries.Count());

        // check GET with onlyActive
        getEntries = await Get<GatewayViewModelGet[]>(client, UrlForGetMultiple(true), HttpStatusCode.OK);
        Assert.AreEqual(1, getEntries.Count());
        CheckWasCreatedFrom(entryPost, getEntries.Single());

        //reenable entryPostWithDisabled
        entryPostWithDisabled.DisabledAt = null;
        var entryPostKeyDisabled = ExtractPostKey(entryPostWithDisabled);
        await Put(client, UrlForKey(entryPostKeyDisabled), entryPostWithDisabled, HttpStatusCode.NoContent);
        // check GET with onlyActive
        getEntries = await Get<GatewayViewModelGet[]>(client, UrlForGetMultiple(true), HttpStatusCode.OK);
        Assert.AreEqual(2, getEntries.Count());
      }
    }

  }
}
