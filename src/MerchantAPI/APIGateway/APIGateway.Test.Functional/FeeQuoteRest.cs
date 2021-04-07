// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.APIGateway.Domain;
using MerchantAPI.APIGateway.Domain.Repositories;
using MerchantAPI.APIGateway.Infrastructure.Repositories;
using MerchantAPI.APIGateway.Rest.ViewModels;
using MerchantAPI.APIGateway.Test.Functional.Mock;
using MerchantAPI.APIGateway.Test.Functional.Server;
using MerchantAPI.Common.Authentication;
using MerchantAPI.Common.Clock;
using MerchantAPI.Common.Test;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;

namespace MerchantAPI.APIGateway.Test.Functional
{
  [TestClass]
  public class FeeQuoteRest : CommonRestMethodsBase<FeeQuoteConfigViewModelGet, FeeQuoteViewModelCreate, AppSettings> 
  {
    public override string LOG_CATEGORY { get { return "MerchantAPI.APIGateway.Test.Functional"; } }
    public override string DbConnectionString { get { return Configuration["ConnectionStrings:DBConnectionString"]; } }
    public string DbConnectionStringDDL { get { return Configuration["ConnectionStrings:DBConnectionStringDDL"]; } }

    public override TestServer CreateServer(bool mockedServices, TestServer serverCallback, string dbConnectionString)
    {
        return new TestServerBase(DbConnectionStringDDL).CreateServer<MapiServer, APIGatewayTestsMockStartup, APIGatewayTestsStartup>(mockedServices, serverCallback, dbConnectionString);
    }

    public FeeQuoteRepositoryPostgres FeeQuoteRepository { get; private set; }

    [TestInitialize]
    public void TestInitialize()
    {
      Initialize(mockedServices: false);
      ApiKeyAuthentication = AppSettings.RestAdminAPIKey;

      FeeQuoteRepository = server.Services.GetRequiredService<IFeeQuoteRepository>() as FeeQuoteRepositoryPostgres;
      FeeQuoteRepositoryMock.quoteExpiryMinutes = 10;
    }

    [TestCleanup]
    public void TestCleanup()
    {
      Cleanup();
    }

    public override string GetNonExistentKey() => "-1";
    public override string GetBaseUrl() => MapiServer.ApiMapiFeeQuoteConfigUrl;
    public override string ExtractGetKey(FeeQuoteConfigViewModelGet entry) => entry.Id.ToString();
    public override string ExtractPostKey(FeeQuoteViewModelCreate entry) => entry.Id.ToString();

    public override void SetPostKey(FeeQuoteViewModelCreate entry, string key)
    {
      entry.Id = long.Parse(key);
    }

    public override FeeQuoteViewModelCreate GetItemToCreate()
    {
      return new FeeQuoteViewModelCreate
      {
        Id = 1,
        ValidFrom = DateTime.UtcNow.AddSeconds(1),
        Fees = new[] {
              new FeeViewModelCreate {
                FeeType = Const.FeeType.Standard,
                MiningFee = new FeeAmountViewModelCreate {
                  Satoshis = 500,
                  Bytes = 1000
                },
                RelayFee = new FeeAmountViewModelCreate {
                  Satoshis = 250,
                  Bytes = 1000
                },
              },
              new FeeViewModelCreate {
                FeeType = Const.FeeType.Data,
                MiningFee = new FeeAmountViewModelCreate {
                  Satoshis = 250,
                  Bytes = 1000
                },
                RelayFee = new FeeAmountViewModelCreate {
                  Satoshis = 150,
                  Bytes = 1000
                },
              },
          }
      };
    }

    private FeeQuoteViewModelCreate GetItemToCreateWithIdentity()
    {
      return new FeeQuoteViewModelCreate
      {
        Id = 1,
        ValidFrom = MockedClock.UtcNow.AddSeconds(5),
        Fees = new[] {
              new FeeViewModelCreate {
                FeeType = Const.FeeType.Standard,
                MiningFee = new FeeAmountViewModelCreate {
                  Satoshis = 500,
                  Bytes = 1000
                },
                RelayFee = new FeeAmountViewModelCreate {
                  Satoshis = 250,
                  Bytes = 1000
                },
              },
              new FeeViewModelCreate {
                FeeType = Const.FeeType.Data,
                MiningFee = new FeeAmountViewModelCreate {
                  Satoshis = 250,
                  Bytes = 1000
                },
                RelayFee = new FeeAmountViewModelCreate {
                  Satoshis = 150,
                  Bytes = 1000
                },
              },
          },
        Identity = this.GetMockedIdentity.Identity,
        IdentityProvider = this.GetMockedIdentity.IdentityProvider
      };
    }

    public override FeeQuoteViewModelCreate[] GetItemsToCreate()
    {
      return new[] {
             new FeeQuoteViewModelCreate
             {
                Id = 1,
                ValidFrom = MockedClock.UtcNow.AddSeconds(1),
                Fees = new[] {
                  new FeeViewModelCreate {
                    FeeType = Const.FeeType.Standard,
                    MiningFee = new FeeAmountViewModelCreate {
                      Satoshis = 100,
                      Bytes = 1000
                    },
                    RelayFee = new FeeAmountViewModelCreate {
                      Satoshis = 150,
                      Bytes = 1000
                    },
                  },
                }
             },
             new FeeQuoteViewModelCreate
             {
                Id = 2,
                ValidFrom = MockedClock.UtcNow.AddSeconds(1),
                Fees = new[] {
                  new FeeViewModelCreate {
                    FeeType = Const.FeeType.Standard,
                    MiningFee = new FeeAmountViewModelCreate {
                      Satoshis = 200,
                      Bytes = 1000
                    },
                    RelayFee = new FeeAmountViewModelCreate {
                      Satoshis = 150,
                      Bytes = 1000
                    },
                  }
                }
             }
      };
    }


    public override void CheckWasCreatedFrom(FeeQuoteViewModelCreate post, FeeQuoteConfigViewModelGet get)
    {
      Assert.AreEqual(post.Id, get.Id);
      if (post.ValidFrom.HasValue)
      {
        Assert.IsTrue(Math.Abs((post.ValidFrom.Value.Subtract(get.ValidFrom.Value.ToUniversalTime())).TotalMilliseconds) < 1);
      }
      
      for(int i=0; i<post.Fees.Length; i++)
      {
        var postFee = post.Fees[i].ToDomainObject();
        var getFee = get.Fees.Single(x => x.FeeType == postFee.FeeType);
        Assert.AreEqual(postFee.FeeType, getFee.FeeType);
        Assert.AreEqual(postFee.MiningFee.Bytes, getFee.MiningFee.ToDomainObject(Const.AmountType.MiningFee).Bytes);
        Assert.AreEqual(postFee.MiningFee.Satoshis, getFee.MiningFee.ToDomainObject(Const.AmountType.MiningFee).Satoshis);
        Assert.AreEqual(postFee.RelayFee.Bytes, getFee.RelayFee.ToDomainObject(Const.AmountType.RelayFee).Bytes);
        Assert.AreEqual(postFee.RelayFee.Satoshis, getFee.RelayFee.ToDomainObject(Const.AmountType.RelayFee).Satoshis);
      }

      Assert.AreEqual(post.Identity, get.Identity);
      Assert.AreEqual(post.IdentityProvider, get.IdentityProvider);

    }

    public override void ModifyEntry(FeeQuoteViewModelCreate entry)
    {
      entry.Identity += "Updated identity";
    }

    private string UrlWithIdentity(string url, UserAndIssuer userAndIssuer)
    {
      if (userAndIssuer == null)
      {
        return url;
      }
      url = (!url.Contains("?")) ? url += "?" : url += "&"; 
      List<string> userParams = new List<string>();
      if (userAndIssuer.Identity != null)
      {
        userParams.Add($"identity={HttpUtility.UrlEncode(userAndIssuer.Identity)}");
      }
      if (userAndIssuer.IdentityProvider != null)
      {
        userParams.Add($"identityProvider={HttpUtility.UrlEncode(userAndIssuer.IdentityProvider)}");
      }
      return url + String.Join("&", userParams);
    }

    public string UrlForCurrentFeeQuoteKey(UserAndIssuer userAndIssuer, bool anonymous=false)
    {
      string url = GetBaseUrl() + $"?current=true";
      if (anonymous)
      {
        url += $"&anonymous=true";
      }
      return UrlWithIdentity(url, userAndIssuer);
    }

    public string UrlForValidFeeQuotesKey(UserAndIssuer userAndIssuer, bool anonymous = false)
    {
      string url = GetBaseUrl() + $"?valid=true";
      if (anonymous)
      {
        url += $"&anonymous=true";
      }
      return UrlWithIdentity(url, userAndIssuer);
    }

    [TestMethod]
    public async Task GetByID_CheckFeeAmountsConsistency()
    {
      var entryPost = GetItemToCreate();
      var entryPostKey = ExtractPostKey(entryPost);
      // Create new feeQuote using POST and check created entry
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(client, entryPost, HttpStatusCode.Created);
      var getEntry = await Get<FeeQuoteConfigViewModelGet>(client, UrlForKey(entryPostKey), HttpStatusCode.OK);
      CheckWasCreatedFrom(entryPost, getEntry);

      // feeQuoteDb is loaded directly from db, should be equal to the one we GET through REST API
      var feeQuoteDb = FeeQuoteRepository.GetFeeQuoteById(long.Parse(entryPostKey), false);
      FeeQuoteConfigViewModelGet getEntryVm = new FeeQuoteConfigViewModelGet(feeQuoteDb);
      CheckWasCreatedFrom(entryPost, getEntryVm);
      // getEntryVm should also have same order of fees
      Assert.IsTrue(getEntry.Fees.First().FeeType == getEntryVm.Fees.First().FeeType);
      Assert.IsTrue(getEntry.Fees.Last().FeeType == getEntryVm.Fees.Last().FeeType);

      // we check if miningFee and relayFee are correctly loaded from db
      // if we select feeAmounts ordered by DESC (inside JOIN query)
      var feeQuoteDbDesc = FeeQuoteRepository.GetFeeQuoteById(long.Parse(entryPostKey), true);
      getEntryVm = new FeeQuoteConfigViewModelGet(feeQuoteDbDesc);
      // getEntryVm should should have different order of fees from getEntry
      Assert.IsTrue(getEntry.Fees.First().FeeType == getEntryVm.Fees.Last().FeeType);
      Assert.IsTrue(getEntry.Fees.Last().FeeType == getEntryVm.Fees.First().FeeType);
      // feeAmounts consistency is checked inside CheckWasCreatedFrom
      CheckWasCreatedFrom(entryPost, getEntryVm);
    }

    [TestMethod]
    public async Task TestPostEmptyValidFrom()
    {
      var entryPost = GetItemToCreate();
      entryPost.ValidFrom = null;

      var entryPostKey = ExtractPostKey(entryPost);

      // Create new one using POST
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(client, entryPost, HttpStatusCode.Created);

      // And we should be able to retrieve the entry through GET
      var get2 = await Get<FeeQuoteConfigViewModelGet>(client, UrlForKey(entryPostKey), HttpStatusCode.OK);

      // And entry returned by POST should be the same as entry returned by GET
      CheckWasCreatedFrom(entryPost, get2);

      // validFrom is filled
      Assert.IsTrue(get2.CreatedAt <= get2.ValidFrom.Value);
    }

    [TestMethod]
    public async Task TestPostInvalidFee_Satoshis()
    {
      var entryPost = GetItemToCreate();
      // Create new one using POST
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(client, entryPost, HttpStatusCode.Created);

      entryPost = GetItemToCreate();
      foreach (var fee in entryPost.Fees)
      {
        fee.MiningFee.Satoshis = 0;
        fee.RelayFee.Satoshis = 0;
      }
      // Create new one using POST
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(client, entryPost, HttpStatusCode.Created);

      entryPost = GetItemToCreate();
      //set invalid minning fee value
      entryPost.Fees.First().MiningFee.Satoshis = -1;
      // Create new one using POST - should return badRequest
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(client, entryPost, HttpStatusCode.BadRequest);

      entryPost = GetItemToCreate();
      //set invalid relay fee value
      entryPost.Fees.First().RelayFee.Satoshis = -1;
      // Create new one using POST - should return badRequest
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(client, entryPost, HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task TestPostInvalidFee_Bytes()
    {
      var entryPost = GetItemToCreate();

      // Create new one using POST
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(client, entryPost, HttpStatusCode.Created);

      entryPost = GetItemToCreate();
      //set invalid minning fee value
      entryPost.Fees.First().MiningFee.Bytes = 0;
      // Create new one using POST - should return badRequest
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(client, entryPost, HttpStatusCode.BadRequest);

      entryPost = GetItemToCreate();
      //set invalid minning fee value
      entryPost.Fees.First().RelayFee.Bytes = 0;
      // Create new one using POST - should return badRequest
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(client, entryPost, HttpStatusCode.BadRequest);

      entryPost = GetItemToCreate();
      //set invalid minning fee value
      entryPost.Fees.First().MiningFee.Bytes = -1;
      // Create new one using POST - should return badRequest
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(client, entryPost, HttpStatusCode.BadRequest);

      entryPost = GetItemToCreate();
      //set invalid relay fee value
      entryPost.Fees.First().RelayFee.Bytes = -1;
      // Create new one using POST - should return badRequest
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(client, entryPost, HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task TestPostOldValidFrom()
    {
      var entryPost = GetItemToCreate();
      entryPost.ValidFrom = DateTime.UtcNow;

      // Create new one using POST - should return badRequest
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(client, entryPost, HttpStatusCode.BadRequest);

      entryPost.ValidFrom = DateTime.UtcNow.AddDays(1); // should succeed
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(client, entryPost, HttpStatusCode.Created);

    }

    [TestMethod]
    public override async Task Put() 
    {
      var entryPost = GetItemToCreate();
      var entryPostKey = ExtractPostKey(entryPost);

      // Check that id does not exists (database is deleted at start of test)
      await Get<FeeQuoteConfigViewModelGet>(client, UrlForKey(entryPostKey), HttpStatusCode.NotFound);

      // we do not support put action ...
      await Put(client, UrlForKey(entryPostKey), entryPost, HttpStatusCode.MethodNotAllowed);
    }

    [TestMethod]
    public override async Task DeleteTest()
    {
      var entries = GetItemsToCreate();

      foreach (var entry in entries)
      {
        // Create new one using POST
        await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(client, entry, HttpStatusCode.Created);
      }

      // Check if all are there
      foreach (var entry in entries)
      {
        // Create new one using POST
        await Get<FeeQuoteConfigViewModelGet>(client, UrlForKey(ExtractPostKey(entry)), HttpStatusCode.OK);
      }

      var firstKey = ExtractPostKey(entries.First());

      // Delete first one - we do not support delete action
      await Delete(client, UrlForKey(firstKey), HttpStatusCode.MethodNotAllowed);

    }

    [TestMethod]
    public override async Task Delete_NoElement_ShouldReturnNoContent()
    {
      // Delete - we do not support delete action
      await Delete(client, UrlForKey(GetNonExistentKey()), HttpStatusCode.MethodNotAllowed);
    }

    [TestMethod]
    public override async Task MultiplePost()
    {
      var entryPost = GetItemToCreate();

      var entryPostKey = ExtractPostKey(entryPost);

      // Check that id does not exists (database is deleted at start of test)
      await Get<FeeQuoteConfigViewModelGet>(client, UrlForKey(entryPostKey), HttpStatusCode.NotFound);


      // Create new one using POST
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(client, entryPost, HttpStatusCode.Created);

      // Try to create it again - it will not fail, because createdAt differs
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(client, entryPost, HttpStatusCode.Created); 
    }

    [TestMethod]
    public override async Task TestPost_2x_ShouldReturn409()
    {
      var entryPost = GetItemToCreate();

      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(client, entryPost, HttpStatusCode.Created);
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(client, entryPost, HttpStatusCode.Created);

      // does not fail with conflict, because createdAt differs for miliseconds
    }


    [TestMethod]
    public async Task TestPost_WithInvalidAuthentication()
    {
      ApiKeyAuthentication = null;
      RestAuthentication = "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiI1IiwibmJmIjoxNTk5NDExNDQzLCJleHAiOjE5MTQ3NzE0NDMsImlhdCI6MTU5OTQxMTQ0MywiaXNzIjoiaHR0cDovL215c2l0ZS5jb20iLCJhdWQiOiJodHRwOi8vbXlhdWRpZW5jZS5jb20ifQ.Z43NASAbIxMZrL2MzbJTJD30hYCxhoAs-8heDjQMnjM";
      var entryPost = GetItemToCreate();
      var (_, _) = await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(client, entryPost, HttpStatusCode.Unauthorized);

      ApiKeyAuthentication = AppSettings.RestAdminAPIKey;
      RestAuthentication = null;
      (_, _) = await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(client, entryPost, HttpStatusCode.Created);
    }

    [TestMethod]
    public async Task InvalidPost()
    {
      // Try to create it - it should fail
      var entryPost = GetItemToCreate();
      entryPost.Fees = null;
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(client, entryPost, HttpStatusCode.BadRequest);

      // test invalid identity
      entryPost = GetItemToCreate();
      entryPost.Identity = "test";
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(client, entryPost, HttpStatusCode.BadRequest);

      entryPost = GetItemToCreate();
      entryPost.IdentityProvider = "testProvider";
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(client, entryPost, HttpStatusCode.BadRequest);

      entryPost = GetItemToCreateWithIdentity();
      entryPost.Identity = "";
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(client, entryPost, HttpStatusCode.BadRequest);

      entryPost = GetItemToCreate();
      entryPost.IdentityProvider = "  ";
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(client, entryPost, HttpStatusCode.BadRequest);

      // test invalid fees
      entryPost = GetItemToCreate();
      entryPost.Fees = new FeeViewModelCreate[0];
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(client, entryPost, HttpStatusCode.BadRequest);

      entryPost = GetItemToCreate();
      entryPost.Fees[0].FeeType = null;
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(client, entryPost, HttpStatusCode.BadRequest);

      entryPost = GetItemToCreate();
      entryPost.Fees[0].MiningFee = null;
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(client, entryPost, HttpStatusCode.BadRequest);

      entryPost = GetItemToCreate();
      entryPost.Fees[0].MiningFee.Satoshis = -1;
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(client, entryPost, HttpStatusCode.BadRequest);

      entryPost = GetItemToCreate();
      entryPost.Fees[0].RelayFee.Bytes = -1;
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(client, entryPost, HttpStatusCode.BadRequest);

      entryPost = GetItemToCreate();
      entryPost.Fees[1].FeeType = entryPost.Fees[0].FeeType;
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(client, entryPost, HttpStatusCode.BadRequest);

      // only successful call
      entryPost = GetItemToCreate();
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(client, entryPost, HttpStatusCode.Created);
      // check GET all
      var getEntries = await Get<FeeQuoteConfigViewModelGet[]>(client, GetBaseUrl(), HttpStatusCode.OK);
      Assert.AreEqual(1, getEntries.Count());
    }

    [TestMethod]
    public async Task TestFeeQuotesValidGetParameters()
    {
      // arrange
      var entryPostWithIdentity = GetItemToCreateWithIdentity();
      var (entryResponsePostIdentity, _) = await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(client, entryPostWithIdentity, HttpStatusCode.Created);

      CheckWasCreatedFrom(entryPostWithIdentity, entryResponsePostIdentity);

      var entryPost = GetItemToCreate();
      entryPost.Id = 2;
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(client, entryPost, HttpStatusCode.Created);
      entryPost.Id = 3;
      var (entryResponsePost, _) = await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(client, entryPost, HttpStatusCode.Created);

      // act
      using (MockedClock.NowIs(entryResponsePost.CreatedAt.AddSeconds(10)))
      {
        // check GET for identity
        var getEntries = await Get<FeeQuoteConfigViewModelGet[]>(client, UrlForValidFeeQuotesKey(GetMockedIdentity), HttpStatusCode.OK);
        CheckWasCreatedFrom(entryPostWithIdentity, getEntries.Single());

        // check GET for identityProvider
        var tIdentity = GetMockedIdentity;
        tIdentity.Identity = null;
        getEntries = await Get<FeeQuoteConfigViewModelGet[]>(client, UrlForValidFeeQuotesKey(tIdentity), HttpStatusCode.OK);
        CheckWasCreatedFrom(entryPostWithIdentity, getEntries.Single());

        // check GET for identity
        tIdentity = GetMockedIdentity;
        tIdentity.IdentityProvider = null;
        getEntries = await Get<FeeQuoteConfigViewModelGet[]>(client, UrlForValidFeeQuotesKey(tIdentity), HttpStatusCode.OK);
        CheckWasCreatedFrom(entryPostWithIdentity, getEntries.Single());

        // check GET for anonymous
        getEntries = await Get<FeeQuoteConfigViewModelGet[]>(client, UrlForValidFeeQuotesKey(null)+$"&anonymous=true", HttpStatusCode.OK);
        Assert.AreEqual(2, getEntries.Count());

        // check GET for identity+anonymous
        getEntries = await Get<FeeQuoteConfigViewModelGet[]>(client, UrlForValidFeeQuotesKey(GetMockedIdentity) + $"&anonymous=true", HttpStatusCode.OK);
        Assert.AreEqual(3, getEntries.Count());

        // check GET all
        getEntries = await Get<FeeQuoteConfigViewModelGet[]>(client, UrlForValidFeeQuotesKey(null), HttpStatusCode.OK);
        Assert.AreEqual(3, getEntries.Count());
      }

    }


    [TestMethod]
    public async Task TestFeeQuotesCurrentAndValidDifferentCreatedAt()
    {
      // arrange
      var validFrom = new DateTime(2020, 9, 16, 6, (int)FeeQuoteRepositoryMock.quoteExpiryMinutes, 0);
      using (MockedClock.NowIs(new DateTime(2020, 9, 16, 6, 0, 0)))
      {
        var entryPost = GetItemToCreate();
        entryPost.Id = 1;
        entryPost.ValidFrom = validFrom;
        await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(client, entryPost, HttpStatusCode.Created);
      }
      using (MockedClock.NowIs(new DateTime(2020, 9, 16, 6, (int)(FeeQuoteRepositoryMock.quoteExpiryMinutes * 0.8), 0)))
      {
        var entryPost = GetItemToCreate();
        entryPost.Id = 2;
        entryPost.ValidFrom = validFrom;
        await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(client, entryPost, HttpStatusCode.Created);
      }

      // act
      using (MockedClock.NowIs(new DateTime(2020, 9, 16, 6, (int)(FeeQuoteRepositoryMock.quoteExpiryMinutes * 0.5), 0)))
      {
        // check GET for anonymous
        var getEntries = await Get<FeeQuoteConfigViewModelGet[]>(client, UrlForValidFeeQuotesKey(null) + $"&anonymous=true", HttpStatusCode.OK);
        Assert.AreEqual(0, getEntries.Count());

        getEntries = await Get<FeeQuoteConfigViewModelGet[]>(client, UrlForCurrentFeeQuoteKey(null, anonymous: true), HttpStatusCode.OK);
        Assert.AreEqual(0, getEntries.Count());
      }

      using (MockedClock.NowIs(new DateTime(2020, 9, 16, 6, (int)(FeeQuoteRepositoryMock.quoteExpiryMinutes * 1.2), 0)))
      {
        // check GET for anonymous
        var getEntries = await Get<FeeQuoteConfigViewModelGet[]>(client, UrlForValidFeeQuotesKey(null) + $"&anonymous=true", HttpStatusCode.OK);
        Assert.AreEqual(1, getEntries.Count());

        getEntries = await Get<FeeQuoteConfigViewModelGet[]>(client, UrlForCurrentFeeQuoteKey(null, anonymous: true), HttpStatusCode.OK);
        Assert.AreEqual(2, getEntries.Single().Id);
      }

      using (MockedClock.NowIs(new DateTime(2020, 9, 16, 6, (int)(FeeQuoteRepositoryMock.quoteExpiryMinutes*2.1), 0)))
      {
        // check GET for anonymous
        var getEntries = await Get<FeeQuoteConfigViewModelGet[]>(client, UrlForValidFeeQuotesKey(null) + $"&anonymous=true", HttpStatusCode.OK);
        Assert.AreEqual(1, getEntries.Count());

        getEntries = await Get<FeeQuoteConfigViewModelGet[]>(client, UrlForCurrentFeeQuoteKey(null, anonymous: true), HttpStatusCode.OK);
        Assert.AreEqual(2, getEntries.Single().Id);
      }

    }


    [TestMethod]
    public async Task TestFeeQuotesValidOverExpiryGetParameters()
    {
      // arrange
      var entryPostWithIdentity = GetItemToCreateWithIdentity();
      var (entryResponsePostIdentity, _) = await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(client, entryPostWithIdentity, HttpStatusCode.Created);

      CheckWasCreatedFrom(entryPostWithIdentity, entryResponsePostIdentity);

      var entryPost = GetItemToCreate();
      entryPost.Id = 2;
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(client, entryPost, HttpStatusCode.Created);
      entryPost.Id = 3;
      var (entryResponsePost, _) = await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(client, entryPost, HttpStatusCode.Created);

      // act
      using (MockedClock.NowIs(entryResponsePost.CreatedAt.AddMinutes(FeeQuoteRepositoryMock.quoteExpiryMinutes*2)))
      {
        // check GET for identity
        var getEntries = await Get<FeeQuoteConfigViewModelGet[]>(client, UrlForValidFeeQuotesKey(GetMockedIdentity), HttpStatusCode.OK);
        CheckWasCreatedFrom(entryPostWithIdentity, getEntries.Single());

        // check GET for anonymous
        getEntries = await Get<FeeQuoteConfigViewModelGet[]>(client, UrlForValidFeeQuotesKey(null) + $"&anonymous=true", HttpStatusCode.OK);
        Assert.AreEqual(1, getEntries.Count());
        Assert.AreEqual(entryPost.Id, getEntries.Single().Id);

        // check GET for identity+anonymous
        getEntries = await Get<FeeQuoteConfigViewModelGet[]>(client, UrlForValidFeeQuotesKey(GetMockedIdentity) + $"&anonymous=true", HttpStatusCode.OK);
        Assert.AreEqual(2, getEntries.Count());

        // check GET all
        getEntries = await Get<FeeQuoteConfigViewModelGet[]>(client, UrlForValidFeeQuotesKey(null), HttpStatusCode.OK);
        Assert.AreEqual(2, getEntries.Count());
      }

    }

    [TestMethod]
    public async Task TestFeeQuotesGetParameters()
    {
      // arrange
      var entryPostWithIdentity = GetItemToCreateWithIdentity();

      var (entryResponsePostIdentity, _) = await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(client, entryPostWithIdentity, HttpStatusCode.Created);

      CheckWasCreatedFrom(entryPostWithIdentity, entryResponsePostIdentity);

      var entryPost = GetItemToCreate();
      entryPost.Id = 2;
      var (entryResponsePost, _) = await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(client, entryPost, HttpStatusCode.Created);

      // act
      using (MockedClock.NowIs(entryResponsePost.CreatedAt.AddMinutes(-FeeQuoteRepositoryMock.quoteExpiryMinutes)))
      {
        // check GET for identity & identityProvider
        var getEntries = await Get<FeeQuoteConfigViewModelGet[]>(client, UrlWithIdentity(GetBaseUrl(), GetMockedIdentity), HttpStatusCode.OK);
        CheckWasCreatedFrom(entryPostWithIdentity, getEntries.Single());

        // check GET for identityProvider
        var tIdentity = GetMockedIdentity;
        tIdentity.Identity = null;
        getEntries = await Get<FeeQuoteConfigViewModelGet[]>(client, UrlWithIdentity(GetBaseUrl(), tIdentity), HttpStatusCode.OK);
        CheckWasCreatedFrom(entryPostWithIdentity, getEntries.Single());

        // check GET for identity
        tIdentity = GetMockedIdentity;
        tIdentity.IdentityProvider = null;
        getEntries = await Get<FeeQuoteConfigViewModelGet[]>(client, UrlWithIdentity(GetBaseUrl(), tIdentity), HttpStatusCode.OK);
        CheckWasCreatedFrom(entryPostWithIdentity, getEntries.Single());

        // check GET for anonymous
        getEntries = await Get<FeeQuoteConfigViewModelGet[]>(client, UrlWithIdentity(GetBaseUrl(), null) + $"?anonymous=true", HttpStatusCode.OK);
        CheckWasCreatedFrom(entryPost, getEntries.Single());

        // check GET for identity+anonymous
        getEntries = await Get<FeeQuoteConfigViewModelGet[]>(client, UrlWithIdentity(GetBaseUrl(), GetMockedIdentity) + $"&anonymous=true", HttpStatusCode.OK);
        Assert.AreEqual(2, getEntries.Count());

        // check GET all
        getEntries = await Get<FeeQuoteConfigViewModelGet[]>(client, UrlWithIdentity(GetBaseUrl(), null), HttpStatusCode.OK);
        Assert.AreEqual(2, getEntries.Count());
      }

    }

    [TestMethod]
    public async Task TestPost_2x_GetCurrentFeeQuote()
    {
      var entryPost = GetItemToCreate();
      var (entryResponsePost, _) = await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(client, entryPost, HttpStatusCode.Created);
      var (entryResponsePost2, _) = await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(client, entryPost, HttpStatusCode.Created);

      Assert.IsTrue(entryResponsePost.CreatedAt < entryResponsePost2.CreatedAt);

      using (MockedClock.NowIs(entryResponsePost.CreatedAt.AddMinutes(-1)))
      {
        var getEntries = await Get<FeeQuoteConfigViewModelGet[]>(client, UrlForCurrentFeeQuoteKey(null, anonymous: true), HttpStatusCode.OK);
        Assert.AreEqual(0, getEntries.Count());
      }

      using (MockedClock.NowIs(entryResponsePost.CreatedAt.AddMinutes(1)))
      {
        // current feeQuote should return newer
        var getEntries = await Get<FeeQuoteConfigViewModelGet[]>(client, UrlForCurrentFeeQuoteKey(null, anonymous: true), HttpStatusCode.OK);
        Assert.AreEqual(entryResponsePost2.Id, getEntries.Single().Id);
      }

    }

    [TestMethod]
    public async Task TestGetValidFeeQuotes()
    {
      DateTime tNow = DateTime.UtcNow;
      var entries = GetItemsToCreate();


      entries.Last().ValidFrom = entries.First().ValidFrom.Value.AddMinutes(FeeQuoteRepositoryMock.quoteExpiryMinutes/2);
      foreach (var entry in entries)
      {
        // Create new one using POST
        await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(client, entry, HttpStatusCode.Created);
      }

      using (MockedClock.NowIs(tNow.AddMinutes(-FeeQuoteRepositoryMock.quoteExpiryMinutes)))
      {
        // Should return no results - no feeQuote is yet valid
        var getEntriesInPast = await Get<FeeQuoteConfigViewModelGet[]>(client,
          UrlForValidFeeQuotesKey(null), HttpStatusCode.OK);
        Assert.AreEqual(0, getEntriesInPast.Length);
      }

      using (MockedClock.NowIs(tNow.AddMinutes(1)))
      {
        // We should be able to retrieve first:
        var getEntries = await Get<FeeQuoteConfigViewModelGet[]>(client,
          UrlForValidFeeQuotesKey(null), HttpStatusCode.OK);
        Assert.AreEqual(1, getEntries.Length);
        CheckWasCreatedFrom(entries[0], getEntries[0]);
      }

      using (MockedClock.NowIs(tNow.AddMinutes((FeeQuoteRepositoryMock.quoteExpiryMinutes / 2) + 1)))
      {
        // We should be able to retrieve both:
        var getEntries = await Get<FeeQuoteConfigViewModelGet[]>(client,
          UrlForValidFeeQuotesKey(null), HttpStatusCode.OK);
        Assert.AreEqual(2, getEntries.Length);
      }

      using (MockedClock.NowIs(entries.Last().ValidFrom.Value.AddMinutes(FeeQuoteRepositoryMock.quoteExpiryMinutes*2)))
      {
        // We should be able to retrieve second:
        var getEntries = await Get<FeeQuoteConfigViewModelGet[]>(client,
          UrlForValidFeeQuotesKey(null), HttpStatusCode.OK);
        Assert.AreEqual(1, getEntries.Length);
        CheckWasCreatedFrom(entries[1], getEntries[0]);
      }

    }

    [TestMethod]
    public async Task TestFeeQuotesForSimilarIdentities()
    {
      // arrange
      var entryPostWithIdentity = GetItemToCreateWithIdentity();
      var testIdentity = GetMockedIdentity;
      testIdentity.Identity = "test ";
      entryPostWithIdentity.Identity = testIdentity.Identity;
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(client, entryPostWithIdentity, HttpStatusCode.Created);

      var entryPostWithIdentity2 = GetItemToCreateWithIdentity();
      entryPostWithIdentity2.Identity = "test _ underline";
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(client, entryPostWithIdentity2, HttpStatusCode.Created);

      // test if we properly check for keys in cache
      using (MockedClock.NowIs(DateTime.UtcNow.AddMinutes(1)))
      {
        testIdentity.IdentityProvider = null;
        var getEntries = await Get<FeeQuoteConfigViewModelGet[]>(client,
          UrlForValidFeeQuotesKey(testIdentity), HttpStatusCode.OK);
        Assert.AreEqual(1, getEntries.Length); // must be only one
        CheckWasCreatedFrom(entryPostWithIdentity, getEntries[0]);
      }
    }

    [TestMethod]
    public async Task TestFeeQuotesForSimilarIdentitiesAndProviders()
    {
      // arrange
      var entryPostWithIdentity = GetItemToCreateWithIdentity();
      entryPostWithIdentity.Identity = "test_";
      entryPostWithIdentity.IdentityProvider = "underline";
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(client, entryPostWithIdentity, HttpStatusCode.Created);

      var entryPostWithIdentity2 = GetItemToCreateWithIdentity();
      entryPostWithIdentity2.Id = 2;
      entryPostWithIdentity2.Identity = "test";
      entryPostWithIdentity2.IdentityProvider = "_underline";
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(client, entryPostWithIdentity2, HttpStatusCode.Created);

      // test if we properly check for keys in cache
      using (MockedClock.NowIs(DateTime.UtcNow.AddMinutes(1)))
      {
        var getEntries = await Get<FeeQuoteConfigViewModelGet[]>(client,
             UrlForCurrentFeeQuoteKey(new UserAndIssuer()
             {
               Identity = entryPostWithIdentity.Identity,
               IdentityProvider = entryPostWithIdentity.IdentityProvider
             }), HttpStatusCode.OK);
        CheckWasCreatedFrom(entryPostWithIdentity, getEntries.Single());

        getEntries = await Get<FeeQuoteConfigViewModelGet[]>(client,
                     UrlForCurrentFeeQuoteKey(new UserAndIssuer() { 
                       Identity = entryPostWithIdentity2.Identity, 
                       IdentityProvider = entryPostWithIdentity2.IdentityProvider
                     }), HttpStatusCode.OK); 
        CheckWasCreatedFrom(entryPostWithIdentity2, getEntries.Single());
      }
    }
  }
}
