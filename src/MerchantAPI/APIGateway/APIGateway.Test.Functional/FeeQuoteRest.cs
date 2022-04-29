// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain;
using MerchantAPI.APIGateway.Domain.Repositories;
using MerchantAPI.APIGateway.Infrastructure.Repositories;
using MerchantAPI.APIGateway.Rest.ViewModels;
using MerchantAPI.APIGateway.Test.Functional.Server;
using MerchantAPI.Common.Authentication;
using MerchantAPI.Common.Test.Clock;
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
using System.Text.Json;
using MerchantAPI.Common.Json;
using MerchantAPI.APIGateway.Domain.Models;

namespace MerchantAPI.APIGateway.Test.Functional
{
  [TestCategory("TestCategoryNo1")]
  [TestClass]
  public class FeeQuoteRest : CommonRestMethodsBase<FeeQuoteConfigViewModelGet, FeeQuoteViewModelCreate, AppSettings>
  {
    public override string LOG_CATEGORY { get { return "MerchantAPI.APIGateway.Test.Functional"; } }
    public override string DbConnectionString { get { return Configuration["ConnectionStrings:DBConnectionString"]; } }
    public string DbConnectionStringDDL { get { return Configuration["ConnectionStrings:DBConnectionStringDDL"]; } }

    public override TestServer CreateServer(bool mockedServices, TestServer serverCallback, string dbConnectionString, IEnumerable<KeyValuePair<string, string>> overridenSettings = null)
    {
        return new TestServerBase(DbConnectionStringDDL).CreateServer<MapiServer, APIGatewayTestsMockStartup, APIGatewayTestsStartup>(mockedServices, serverCallback, dbConnectionString, overridenSettings);
    }

    public FeeQuoteRepositoryPostgres FeeQuoteRepository { get; private set; }
    public TxRepositoryPostgres TxRepository { get; private set; }

    [TestInitialize]
    public void TestInitialize()
    {
      Initialize(mockedServices: false);
      ApiKeyAuthentication = AppSettings.RestAdminAPIKey;

      FeeQuoteRepository = server.Services.GetRequiredService<IFeeQuoteRepository>() as FeeQuoteRepositoryPostgres;
      TxRepository = server.Services.GetRequiredService<ITxRepository>() as TxRepositoryPostgres;
    }

    [TestCleanup]
    public void TestCleanup()
    {
      Cleanup();
    }

    public override string GetNonExistentKey() => "-1";
    public override string GetBaseUrl() => MapiServer.ApiFeeQuoteConfigUrl;
    public override string ExtractGetKey(FeeQuoteConfigViewModelGet entry) => entry.Id.ToString();
    public override string ExtractPostKey(FeeQuoteViewModelCreate entry) => entry.Id.ToString();

    public override void SetPostKey(FeeQuoteViewModelCreate entry, string key)
    {
      entry.Id = long.Parse(key);
    }

    private static Dictionary<string, object> GetPoliciesDict(string json)
    {
      return HelperTools.JSONDeserialize<Dictionary<string, object>>(json);
    }

    public override FeeQuoteViewModelCreate GetItemToCreate()
    {
      return new FeeQuoteViewModelCreate
      {
        Id = 1,
        ValidFrom = DateTime.UtcNow.AddSeconds(1),
        Policies = GetPoliciesDict("{\"somePolicy\": \"policyValue\"}"),
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
        Policies = GetPoliciesDict("{\"somePolicy\": \"values\"}"),
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
        Identity = this.MockedIdentity.Identity,
        IdentityProvider = this.MockedIdentity.IdentityProvider
      };
    }

    public override FeeQuoteViewModelCreate[] GetItemsToCreate()
    {
      return new[] {
             new FeeQuoteViewModelCreate
             {
                Id = 1,
                ValidFrom = MockedClock.UtcNow.AddSeconds(1),
                Policies = GetPoliciesDict("{\"skipScriptFlags\": \"some flags 1\"}"),
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
                Policies = GetPoliciesDict("{\"skipScriptFlags\": \"some flags 2\"}"),
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

      Assert.IsTrue(post.Policies?.Any() == get.Policies?.Any());
      if (post.Policies != null && post.Policies.Any())
      {
        // compare without extra white spaces
        var serializeOptions = new JsonSerializerOptions { WriteIndented = false };
        var postPoliciesJsonString = JsonSerializer.Serialize(post.Policies, serializeOptions);
        var getPoliciesJsonString = JsonSerializer.Serialize(get.Policies, serializeOptions);
        Assert.AreEqual(postPoliciesJsonString, getPoliciesJsonString);
      }

      for (int i=0; i<post.Fees.Length; i++)
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

    private static string UrlWithIdentity(string url, UserAndIssuer userAndIssuer)
    {
      if (userAndIssuer == null)
      {
        return url;
      }
      url = (!url.Contains("?")) ? url += "?" : url += "&";
      List<string> userParams = new();
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

    public string UrlForCurrentFeeQuoteKey(UserAndIssuer userAndIssuer, bool anonymous = false)
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
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, entryPost, HttpStatusCode.Created);
      var getEntry = await Get<FeeQuoteConfigViewModelGet>(Client, UrlForKey(entryPostKey), HttpStatusCode.OK);
      CheckWasCreatedFrom(entryPost, getEntry);

      // feeQuoteDb is loaded directly from db, should be equal to the one we GET through REST API
      var feeQuoteDb = FeeQuoteRepository.GetFeeQuoteById(long.Parse(entryPostKey), false);
      FeeQuoteConfigViewModelGet getEntryVm = new(feeQuoteDb);
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
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, entryPost, HttpStatusCode.Created);

      // And we should be able to retrieve the entry through GET
      var get2 = await Get<FeeQuoteConfigViewModelGet>(Client, UrlForKey(entryPostKey), HttpStatusCode.OK);

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
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, entryPost, HttpStatusCode.Created);

      entryPost = GetItemToCreate();
      foreach (var fee in entryPost.Fees)
      {
        fee.MiningFee.Satoshis = 0;
        fee.RelayFee.Satoshis = 0;
      }
      // Create new one using POST
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, entryPost, HttpStatusCode.Created);

      entryPost = GetItemToCreate();
      //set invalid minning fee value
      entryPost.Fees.First().MiningFee.Satoshis = -1;
      // Create new one using POST - should return badRequest
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, entryPost, HttpStatusCode.BadRequest);

      entryPost = GetItemToCreate();
      //set invalid relay fee value
      entryPost.Fees.First().RelayFee.Satoshis = -1;
      // Create new one using POST - should return badRequest
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, entryPost, HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task TestPostInvalidFee_Bytes()
    {
      var entryPost = GetItemToCreate();

      // Create new one using POST
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, entryPost, HttpStatusCode.Created);

      entryPost = GetItemToCreate();
      //set invalid minning fee value
      entryPost.Fees.First().MiningFee.Bytes = 0;
      // Create new one using POST - should return badRequest
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, entryPost, HttpStatusCode.BadRequest);

      entryPost = GetItemToCreate();
      //set invalid minning fee value
      entryPost.Fees.First().RelayFee.Bytes = 0;
      // Create new one using POST - should return badRequest
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, entryPost, HttpStatusCode.BadRequest);

      entryPost = GetItemToCreate();
      //set invalid minning fee value
      entryPost.Fees.First().MiningFee.Bytes = -1;
      // Create new one using POST - should return badRequest
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, entryPost, HttpStatusCode.BadRequest);

      entryPost = GetItemToCreate();
      //set invalid relay fee value
      entryPost.Fees.First().RelayFee.Bytes = -1;
      // Create new one using POST - should return badRequest
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, entryPost, HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task TestPostOldValidFrom()
    {
      var entryPost = GetItemToCreate();
      entryPost.ValidFrom = DateTime.UtcNow;

      // Create new one using POST - should return badRequest
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, entryPost, HttpStatusCode.BadRequest);

      entryPost.ValidFrom = DateTime.UtcNow.AddDays(1); // should succeed
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, entryPost, HttpStatusCode.Created);

    }

    [TestMethod]
    public override async Task Put()
    {
      var entryPost = GetItemToCreate();
      var entryPostKey = ExtractPostKey(entryPost);

      // Check that id does not exists (database is deleted at start of test)
      await Get<FeeQuoteConfigViewModelGet>(Client, UrlForKey(entryPostKey), HttpStatusCode.NotFound);

      // we do not support put action ...
      await Put(Client, UrlForKey(entryPostKey), entryPost, HttpStatusCode.MethodNotAllowed);
    }

    [TestMethod]
    public override async Task DeleteTest()
    {
      var entries = GetItemsToCreate();

      foreach (var entry in entries)
      {
        // Create new one using POST
        await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, entry, HttpStatusCode.Created);
      }

      // Check if all are there
      foreach (var entry in entries)
      {
        // Create new one using POST
        await Get<FeeQuoteConfigViewModelGet>(Client, UrlForKey(ExtractPostKey(entry)), HttpStatusCode.OK);
      }

      var firstKey = ExtractPostKey(entries.First());

      // Delete first one - we do not support delete action
      await Delete(Client, UrlForKey(firstKey), HttpStatusCode.MethodNotAllowed);

    }

    [TestMethod]
    public override async Task Delete_NoElement_ShouldReturnNoContent()
    {
      // Delete - we do not support delete action
      await Delete(Client, UrlForKey(GetNonExistentKey()), HttpStatusCode.MethodNotAllowed);
    }

    [TestMethod]
    public override async Task MultiplePost()
    {
      var entryPost = GetItemToCreate();

      var entryPostKey = ExtractPostKey(entryPost);

      // Check that id does not exists (database is deleted at start of test)
      await Get<FeeQuoteConfigViewModelGet>(Client, UrlForKey(entryPostKey), HttpStatusCode.NotFound);


      // Create new one using POST
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, entryPost, HttpStatusCode.Created);

      // Try to create it again - it will not fail, because createdAt differs
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, entryPost, HttpStatusCode.Created);
    }

    [TestMethod]
    public override async Task TestPost_2x_ShouldReturn409()
    {
      var entryPost = GetItemToCreate();

      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, entryPost, HttpStatusCode.Created);
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, entryPost, HttpStatusCode.Created);

      // does not fail with conflict, because createdAt differs for miliseconds
    }


    [TestMethod]
    public async Task TestPost_WithInvalidAuthentication()
    {
      ApiKeyAuthentication = null;
      RestAuthentication = MockedIdentityBearerAuthentication;
      var entryPost = GetItemToCreate();
      var (_, _) = await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, entryPost, HttpStatusCode.Unauthorized);

      ApiKeyAuthentication = AppSettings.RestAdminAPIKey;
      RestAuthentication = null;
      (_, _) = await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, entryPost, HttpStatusCode.Created);
    }

    [TestMethod]
    public async Task InvalidPost()
    {
      // Try to create it - it should fail
      var entryPost = GetItemToCreate();
      entryPost.Fees = null;
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, entryPost, HttpStatusCode.BadRequest);

      // test invalid identity
      entryPost = GetItemToCreate();
      entryPost.Identity = "test";
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, entryPost, HttpStatusCode.BadRequest);

      entryPost = GetItemToCreate();
      entryPost.IdentityProvider = "testProvider";
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, entryPost, HttpStatusCode.BadRequest);

      entryPost = GetItemToCreateWithIdentity();
      entryPost.Identity = "";
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, entryPost, HttpStatusCode.BadRequest);

      entryPost = GetItemToCreate();
      entryPost.IdentityProvider = "  ";
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, entryPost, HttpStatusCode.BadRequest);

      // test invalid fees
      entryPost = GetItemToCreate();
      entryPost.Fees = Array.Empty<FeeViewModelCreate>();
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, entryPost, HttpStatusCode.BadRequest);

      entryPost = GetItemToCreate();
      entryPost.Fees[0].FeeType = null;
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, entryPost, HttpStatusCode.BadRequest);

      entryPost = GetItemToCreate();
      entryPost.Fees[0].MiningFee = null;
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, entryPost, HttpStatusCode.BadRequest);

      entryPost = GetItemToCreate();
      entryPost.Fees[0].MiningFee.Satoshis = -1;
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, entryPost, HttpStatusCode.BadRequest);

      entryPost = GetItemToCreate();
      entryPost.Fees[0].RelayFee.Bytes = -1;
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, entryPost, HttpStatusCode.BadRequest);

      entryPost = GetItemToCreate();
      entryPost.Fees[1].FeeType = entryPost.Fees[0].FeeType;
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, entryPost, HttpStatusCode.BadRequest);

      // only successful call
      entryPost = GetItemToCreate();
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, entryPost, HttpStatusCode.Created);
      // check GET all
      var getEntries = await Get<FeeQuoteConfigViewModelGet[]>(Client, GetBaseUrl(), HttpStatusCode.OK);
      Assert.AreEqual(1, getEntries.Length);
    }

    [TestMethod]
    public async Task TestFeeQuotesValidGetParameters()
    {
      // arrange
      var entryPostWithIdentity = GetItemToCreateWithIdentity();
      var (entryResponsePostIdentity, _) = await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, entryPostWithIdentity, HttpStatusCode.Created);

      CheckWasCreatedFrom(entryPostWithIdentity, entryResponsePostIdentity);

      var entryPost = GetItemToCreate();
      entryPost.Id = 2;
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, entryPost, HttpStatusCode.Created);
      entryPost.Id = 3;
      var (entryResponsePost, _) = await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, entryPost, HttpStatusCode.Created);

      // act
      using (MockedClock.NowIs(entryResponsePost.CreatedAt.AddSeconds(10)))
      {
        // check GET for identity
        var getEntries = await Get<FeeQuoteConfigViewModelGet[]>(Client, UrlForValidFeeQuotesKey(MockedIdentity), HttpStatusCode.OK);
        CheckWasCreatedFrom(entryPostWithIdentity, getEntries.Single());

        // check GET for identityProvider
        var tIdentity = MockedIdentity;
        tIdentity.Identity = null;
        getEntries = await Get<FeeQuoteConfigViewModelGet[]>(Client, UrlForValidFeeQuotesKey(tIdentity), HttpStatusCode.OK);
        CheckWasCreatedFrom(entryPostWithIdentity, getEntries.Single());

        // check GET for identity
        tIdentity = MockedIdentity;
        tIdentity.IdentityProvider = null;
        getEntries = await Get<FeeQuoteConfigViewModelGet[]>(Client, UrlForValidFeeQuotesKey(tIdentity), HttpStatusCode.OK);
        CheckWasCreatedFrom(entryPostWithIdentity, getEntries.Single());

        // check GET for anonymous
        getEntries = await Get<FeeQuoteConfigViewModelGet[]>(Client, UrlForValidFeeQuotesKey(null) + $"&anonymous=true", HttpStatusCode.OK);
        Assert.AreEqual(2, getEntries.Length);

        // check GET for identity+anonymous
        getEntries = await Get<FeeQuoteConfigViewModelGet[]>(Client, UrlForValidFeeQuotesKey(MockedIdentity) + $"&anonymous=true", HttpStatusCode.OK);
        Assert.AreEqual(3, getEntries.Length);

        // check GET all
        getEntries = await Get<FeeQuoteConfigViewModelGet[]>(Client, UrlForValidFeeQuotesKey(null), HttpStatusCode.OK);
        Assert.AreEqual(3, getEntries.Length);
      }

    }


    [TestMethod]
    public async Task TestFeeQuotesCurrentAndValidDifferentCreatedAt()
    {
      // arrange
      var validFrom = new DateTime(2020, 9, 16, 6, (int)AppSettings.QuoteExpiryMinutes, 0);
      using (MockedClock.NowIs(new DateTime(2020, 9, 16, 6, 0, 0)))
      {
        var entryPost = GetItemToCreate();
        entryPost.Id = 1;
        entryPost.ValidFrom = validFrom;
        await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, entryPost, HttpStatusCode.Created);
      }
      using (MockedClock.NowIs(new DateTime(2020, 9, 16, 6, (int)(AppSettings.QuoteExpiryMinutes * 0.8), 0)))
      {
        var entryPost = GetItemToCreate();
        entryPost.Id = 2;
        entryPost.ValidFrom = validFrom;
        await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, entryPost, HttpStatusCode.Created);
      }

      // act
      using (MockedClock.NowIs(new DateTime(2020, 9, 16, 6, (int)(AppSettings.QuoteExpiryMinutes * 0.5), 0)))
      {
        // check GET for anonymous
        var getEntries = await Get<FeeQuoteConfigViewModelGet[]>(Client, UrlForValidFeeQuotesKey(null) + $"&anonymous=true", HttpStatusCode.OK);
        Assert.AreEqual(0, getEntries.Length);

        getEntries = await Get<FeeQuoteConfigViewModelGet[]>(Client, UrlForCurrentFeeQuoteKey(null, anonymous: true), HttpStatusCode.OK);
        Assert.AreEqual(0, getEntries.Length);
      }

      using (MockedClock.NowIs(new DateTime(2020, 9, 16, 6, (int)(AppSettings.QuoteExpiryMinutes * 1.2), 0)))
      {
        // check GET for anonymous
        var getEntries = await Get<FeeQuoteConfigViewModelGet[]>(Client, UrlForValidFeeQuotesKey(null) + $"&anonymous=true", HttpStatusCode.OK);
        Assert.AreEqual(1, getEntries.Length);

        getEntries = await Get<FeeQuoteConfigViewModelGet[]>(Client, UrlForCurrentFeeQuoteKey(null, anonymous: true), HttpStatusCode.OK);
        Assert.AreEqual(2, getEntries.Single().Id);
      }

      using (MockedClock.NowIs(new DateTime(2020, 9, 16, 6, (int)(AppSettings.QuoteExpiryMinutes * 2.1), 0)))
      {
        // check GET for anonymous
        var getEntries = await Get<FeeQuoteConfigViewModelGet[]>(Client, UrlForValidFeeQuotesKey(null) + $"&anonymous=true", HttpStatusCode.OK);
        Assert.AreEqual(1, getEntries.Length);

        getEntries = await Get<FeeQuoteConfigViewModelGet[]>(Client, UrlForCurrentFeeQuoteKey(null, anonymous: true), HttpStatusCode.OK);
        Assert.AreEqual(2, getEntries.Single().Id);
      }

    }


    [TestMethod]
    public async Task TestFeeQuotesValidOverExpiryGetParameters()
    {
      // arrange
      var entryPostWithIdentity = GetItemToCreateWithIdentity();
      var (entryResponsePostIdentity, _) = await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, entryPostWithIdentity, HttpStatusCode.Created);

      CheckWasCreatedFrom(entryPostWithIdentity, entryResponsePostIdentity);

      var entryPost = GetItemToCreate();
      entryPost.Id = 2;
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, entryPost, HttpStatusCode.Created);
      entryPost.Id = 3;
      var (entryResponsePost, _) = await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, entryPost, HttpStatusCode.Created);

      // act
      using (MockedClock.NowIs(entryResponsePost.CreatedAt.AddMinutes(AppSettings.QuoteExpiryMinutes.Value * 2)))
      {
        // check GET for identity
        var getEntries = await Get<FeeQuoteConfigViewModelGet[]>(Client, UrlForValidFeeQuotesKey(MockedIdentity), HttpStatusCode.OK);
        CheckWasCreatedFrom(entryPostWithIdentity, getEntries.Single());

        // check GET for anonymous
        getEntries = await Get<FeeQuoteConfigViewModelGet[]>(Client, UrlForValidFeeQuotesKey(null) + $"&anonymous=true", HttpStatusCode.OK);
        Assert.AreEqual(1, getEntries.Length);
        Assert.AreEqual(entryPost.Id, getEntries.Single().Id);

        // check GET for identity+anonymous
        getEntries = await Get<FeeQuoteConfigViewModelGet[]>(Client, UrlForValidFeeQuotesKey(MockedIdentity) + $"&anonymous=true", HttpStatusCode.OK);
        Assert.AreEqual(2, getEntries.Length);

        // check GET all
        getEntries = await Get<FeeQuoteConfigViewModelGet[]>(Client, UrlForValidFeeQuotesKey(null), HttpStatusCode.OK);
        Assert.AreEqual(2, getEntries.Length);
      }

    }

    [TestMethod]
    public async Task TestFeeQuotesGetParameters()
    {
      // arrange
      var entryPostWithIdentity = GetItemToCreateWithIdentity();

      var (entryResponsePostIdentity, _) = await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, entryPostWithIdentity, HttpStatusCode.Created);

      CheckWasCreatedFrom(entryPostWithIdentity, entryResponsePostIdentity);

      var entryPost = GetItemToCreate();
      entryPost.Id = 2;
      var (entryResponsePost, _) = await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, entryPost, HttpStatusCode.Created);

      // act
      using (MockedClock.NowIs(entryResponsePost.CreatedAt.AddMinutes(-AppSettings.QuoteExpiryMinutes.Value)))
      {
        // check GET for identity & identityProvider
        var getEntries = await Get<FeeQuoteConfigViewModelGet[]>(Client, UrlWithIdentity(GetBaseUrl(), MockedIdentity), HttpStatusCode.OK);
        CheckWasCreatedFrom(entryPostWithIdentity, getEntries.Single());

        // check GET for identityProvider
        var tIdentity = MockedIdentity;
        tIdentity.Identity = null;
        getEntries = await Get<FeeQuoteConfigViewModelGet[]>(Client, UrlWithIdentity(GetBaseUrl(), tIdentity), HttpStatusCode.OK);
        CheckWasCreatedFrom(entryPostWithIdentity, getEntries.Single());

        // check GET for identity
        tIdentity = MockedIdentity;
        tIdentity.IdentityProvider = null;
        getEntries = await Get<FeeQuoteConfigViewModelGet[]>(Client, UrlWithIdentity(GetBaseUrl(), tIdentity), HttpStatusCode.OK);
        CheckWasCreatedFrom(entryPostWithIdentity, getEntries.Single());

        // check GET for anonymous
        getEntries = await Get<FeeQuoteConfigViewModelGet[]>(Client, UrlWithIdentity(GetBaseUrl(), null) + $"?anonymous=true", HttpStatusCode.OK);
        CheckWasCreatedFrom(entryPost, getEntries.Single());

        // check GET for identity+anonymous
        getEntries = await Get<FeeQuoteConfigViewModelGet[]>(Client, UrlWithIdentity(GetBaseUrl(), MockedIdentity) + $"&anonymous=true", HttpStatusCode.OK);
        Assert.AreEqual(2, getEntries.Length);

        // check GET all
        getEntries = await Get<FeeQuoteConfigViewModelGet[]>(Client, UrlWithIdentity(GetBaseUrl(), null), HttpStatusCode.OK);
        Assert.AreEqual(2, getEntries.Length);
      }

    }

    [TestMethod]
    public async Task TestPost_2x_GetCurrentFeeQuote()
    {
      var entryPost = GetItemToCreate();
      var (entryResponsePost, _) = await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, entryPost, HttpStatusCode.Created);
      var (entryResponsePost2, _) = await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, entryPost, HttpStatusCode.Created);

      Assert.IsTrue(entryResponsePost.CreatedAt < entryResponsePost2.CreatedAt);

      using (MockedClock.NowIs(entryResponsePost.CreatedAt.AddMinutes(-1)))
      {
        var getEntries = await Get<FeeQuoteConfigViewModelGet[]>(Client, UrlForCurrentFeeQuoteKey(null, anonymous: true), HttpStatusCode.OK);
        Assert.AreEqual(0, getEntries.Length);
      }

      using (MockedClock.NowIs(entryResponsePost.CreatedAt.AddMinutes(1)))
      {
        // current feeQuote should return newer
        var getEntries = await Get<FeeQuoteConfigViewModelGet[]>(Client, UrlForCurrentFeeQuoteKey(null, anonymous: true), HttpStatusCode.OK);
        Assert.AreEqual(entryResponsePost2.Id, getEntries.Single().Id);
      }

    }

    [TestMethod]
    public async Task TestGetValidFeeQuotes()
    {
      DateTime tNow = DateTime.UtcNow;
      var entries = GetItemsToCreate();


      entries.Last().ValidFrom = entries.First().ValidFrom.Value.AddMinutes(AppSettings.QuoteExpiryMinutes.Value / 2);
      foreach (var entry in entries)
      {
        // Create new one using POST
        await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, entry, HttpStatusCode.Created);
      }

      using (MockedClock.NowIs(tNow.AddMinutes(-AppSettings.QuoteExpiryMinutes.Value)))
      {
        // Should return no results - no feeQuote is yet valid
        var getEntriesInPast = await Get<FeeQuoteConfigViewModelGet[]>(Client,
          UrlForValidFeeQuotesKey(null), HttpStatusCode.OK);
        Assert.AreEqual(0, getEntriesInPast.Length);
      }

      using (MockedClock.NowIs(tNow.AddMinutes(1)))
      {
        // We should be able to retrieve first:
        var getEntries = await Get<FeeQuoteConfigViewModelGet[]>(Client,
          UrlForValidFeeQuotesKey(null), HttpStatusCode.OK);
        Assert.AreEqual(1, getEntries.Length);
        CheckWasCreatedFrom(entries[0], getEntries[0]);
      }

      using (MockedClock.NowIs(tNow.AddMinutes((AppSettings.QuoteExpiryMinutes.Value / 2) + 1)))
      {
        // We should be able to retrieve both:
        var getEntries = await Get<FeeQuoteConfigViewModelGet[]>(Client,
          UrlForValidFeeQuotesKey(null), HttpStatusCode.OK);
        Assert.AreEqual(2, getEntries.Length);
      }

      using (MockedClock.NowIs(entries.Last().ValidFrom.Value.AddMinutes(AppSettings.QuoteExpiryMinutes.Value * 2)))
      {
        // We should be able to retrieve second:
        var getEntries = await Get<FeeQuoteConfigViewModelGet[]>(Client,
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
      var testIdentity = MockedIdentity;
      testIdentity.Identity = "test ";
      entryPostWithIdentity.Identity = testIdentity.Identity;
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, entryPostWithIdentity, HttpStatusCode.Created);

      var entryPostWithIdentity2 = GetItemToCreateWithIdentity();
      entryPostWithIdentity2.Identity = "test _ underline";
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, entryPostWithIdentity2, HttpStatusCode.Created);

      // test if we properly check for keys in cache
      using (MockedClock.NowIs(DateTime.UtcNow.AddMinutes(1)))
      {
        testIdentity.IdentityProvider = null;
        var getEntries = await Get<FeeQuoteConfigViewModelGet[]>(Client,
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
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, entryPostWithIdentity, HttpStatusCode.Created);

      var entryPostWithIdentity2 = GetItemToCreateWithIdentity();
      entryPostWithIdentity2.Id = 2;
      entryPostWithIdentity2.Identity = "test";
      entryPostWithIdentity2.IdentityProvider = "_underline";
      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, entryPostWithIdentity2, HttpStatusCode.Created);

      // test if we properly check for keys in cache
      using (MockedClock.NowIs(DateTime.UtcNow.AddMinutes(1)))
      {
        var getEntries = await Get<FeeQuoteConfigViewModelGet[]>(Client,
             UrlForCurrentFeeQuoteKey(new UserAndIssuer()
             {
               Identity = entryPostWithIdentity.Identity,
               IdentityProvider = entryPostWithIdentity.IdentityProvider
             }), HttpStatusCode.OK);
        CheckWasCreatedFrom(entryPostWithIdentity, getEntries.Single());

        getEntries = await Get<FeeQuoteConfigViewModelGet[]>(Client,
                     UrlForCurrentFeeQuoteKey(new UserAndIssuer() { 
                       Identity = entryPostWithIdentity2.Identity, 
                       IdentityProvider = entryPostWithIdentity2.IdentityProvider
                     }), HttpStatusCode.OK); 
        CheckWasCreatedFrom(entryPostWithIdentity2, getEntries.Single());
      }
    }

    [TestMethod]
    public async Task TestPostNullPolicies()
    {
      var entryPost = GetItemToCreate();
      entryPost.Policies = null;
      var entryPostKey = ExtractPostKey(entryPost);

      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, entryPost, HttpStatusCode.Created);
      var getEntry = await Get<FeeQuoteConfigViewModelGet>(Client, UrlForKey(entryPostKey), HttpStatusCode.OK);
      CheckWasCreatedFrom(entryPost, getEntry);
    }

    [TestMethod]
    [DataRow("\"\"")]
    [DataRow("null")]
    [DataRow("260")]
    [DataRow("[2.60, 2.61]")]
    [DataRow("\"DERSIG=1,MAXTXSIZE=1M\"")]
    [DataRow("{ \"DERSIG\":true, \"MAXTXSIZE\":1000000 }")]
    [DataRow("{ \"subflags\" : [\"flag1\", \"flag2\"]}")]
    public async Task TestPostValidPolicies(string jsonValue)
    {
      var entryPost = GetItemToCreate();
      var domain = entryPost.ToDomainObject(DateTime.UtcNow);
      domain.Policies = "{\"skipScriptFlags\":" + jsonValue + "}";
      var entryPostWithPolicies = new FeeQuoteViewModelCreate(domain)
      {
        Id = entryPost.Id
      };
      var entryPostKey = ExtractPostKey(entryPostWithPolicies);

      await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, entryPostWithPolicies, HttpStatusCode.Created);
      var getEntry = await Get<FeeQuoteConfigViewModelGet>(Client, UrlForKey(entryPostKey), HttpStatusCode.OK);
      CheckWasCreatedFrom(entryPostWithPolicies, getEntry);
    }

    [TestMethod]
    [ExpectedException(typeof(JsonException))]
    [DataRow("")]
    [DataRow("null0")]
    [DataRow("[260, \"261]")]
    public void TestPostInvalidPolicies(string jsonValue)
    {
      var entryPost = GetItemToCreate();
      var domain = entryPost.ToDomainObject(DateTime.UtcNow);
      domain.Policies = "{\"skipScriptFlags\":" + jsonValue + "}";
      _ = new FeeQuoteViewModelCreate(domain);
    }

    [TestMethod]
    public async Task TestDeleteTxsEmpty()
    {
      // no parameters given
      var getEntry = await GetDeleteTxsAsync(HttpStatusCode.BadRequest, null, null);
      Assert.IsNull(getEntry);
      await DeleteTxsAsync(HttpStatusCode.BadRequest, null, null);

      // policyQuote with this id does not exist yet
      getEntry = await GetDeleteTxsAsync(HttpStatusCode.BadRequest, null, 1);
      Assert.IsNull(getEntry);
      await DeleteTxsAsync(HttpStatusCode.BadRequest, null, 1);

      // txstatus sentToNode is reserved for authenticated users
      // after policyQuote for anonymous user is inserted response is still BadRequest
      (var policyQuote, _) = await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, GetItemToCreate(), HttpStatusCode.Created);
      getEntry = await GetDeleteTxsAsync(HttpStatusCode.BadRequest, null, policyQuote.Id);
      await DeleteTxsAsync(HttpStatusCode.BadRequest, null, policyQuote.Id);

      // no policyQuotes with MockedIdentity
      getEntry = await GetDeleteTxsAsync(HttpStatusCode.BadRequest, MockedIdentity, null);
      Assert.IsNull(getEntry);
      await DeleteTxsAsync(HttpStatusCode.BadRequest, MockedIdentity, null);

      // policyQuote with id = 1 has no identityProvider
      getEntry = await GetDeleteTxsAsync(HttpStatusCode.BadRequest, MockedIdentity, policyQuote.Id);
      Assert.IsNull(getEntry);
      await DeleteTxsAsync(HttpStatusCode.BadRequest, MockedIdentity, policyQuote.Id);

      // after policyQuote with MockedIdentity is inserted, RemoveTxs should be successful
      (var policyQuoteWithIdentity, _) = await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, GetItemToCreateWithIdentity(), HttpStatusCode.Created);
      getEntry = await GetDeleteTxsAsync(HttpStatusCode.OK, MockedIdentity, policyQuoteWithIdentity.Id);
      Assert.AreEqual(0, getEntry.Count);
      await DeleteTxsAsync(HttpStatusCode.NoContent, MockedIdentity, policyQuoteWithIdentity.Id);
    }

    private async Task InsertPolicyQuotesAndTxs(FeeQuoteViewModelCreate feeQuoteViewModelCreate)
    {
      (var policyQuote, _) = await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, feeQuoteViewModelCreate, HttpStatusCode.Created);
      (var policyQuote2, _) = await Post<FeeQuoteViewModelCreate, FeeQuoteConfigViewModelGet>(Client, feeQuoteViewModelCreate, HttpStatusCode.Created);

      var txList = new List<Tx>() {
        // txC0 - txC2 can be deleted by admin
        TestBase.CreateNewTx(TestBase.txC0Hash, TestBase.txC0Hex, false, null, true, TxStatus.SentToNode, policyQuoteId: policyQuote.Id),
        TestBase.CreateNewTx(TestBase.txC1Hash, TestBase.txC1Hex, false, null, true, TxStatus.SentToNode, policyQuoteId: policyQuote2.Id),
        // txC2 is specific (CheckFeeDisabled or ConsolidationTx)
        TestBase.CreateNewTx(TestBase.txC2Hash, TestBase.txC2Hex, false, null, true, TxStatus.SentToNode, policyQuoteId: policyQuote2.Id, setPolicyQuote: false),
        // txC3 cannot be deleted by admin, because of the txstatus Mempool
        TestBase.CreateNewTx(TestBase.txC3Hash, TestBase.txC3Hex, false, null, true, TxStatus.Accepted, policyQuoteId: policyQuote.Id),
      };
      await TxRepository.InsertOrUpdateTxsAsync(txList, false);
    }


    [TestMethod]
    public async Task TestDeleteTxs()
    {
      await InsertPolicyQuotesAndTxs(GetItemToCreateWithIdentity());

      // no parameters given (anonymous user)
      var getEntry = await GetDeleteTxsAsync(HttpStatusCode.BadRequest, null, null);
      Assert.IsNull(getEntry);
      await DeleteTxsAsync(HttpStatusCode.BadRequest, null, null);

      // only first tx (txC0) from the list is removed
      getEntry = await GetDeleteTxsAsync(HttpStatusCode.OK, null, 1);
      Assert.AreEqual(1, getEntry.Count);
      Assert.AreEqual(TestBase.txC0Hash, getEntry.TxIds.Single());
      await DeleteTxsAsync(HttpStatusCode.NoContent, null, 1);
      // check if actually removed
      getEntry = await GetDeleteTxsAsync(HttpStatusCode.OK, null, 1);
      Assert.AreEqual(0, getEntry.Count);

      // txC1 and txC2 are removed
      getEntry = await GetDeleteTxsAsync(HttpStatusCode.OK, null, 2);
      Assert.AreEqual(2, getEntry.Count);
      await DeleteTxsAsync(HttpStatusCode.NoContent, null, 2);
      // check if actually removed
      getEntry = await GetDeleteTxsAsync(HttpStatusCode.OK, null, 2);
      Assert.AreEqual(0, getEntry.Count);
    }

    [DataRow(1, 1L)]
    [DataRow(2, 2L)]
    [DataRow(3, null)]
    [TestMethod]
    public async Task TestDeleteTxsWithIdentity(int expectedDeletedTxs, long? policyQuoteId)
    {
      await InsertPolicyQuotesAndTxs(GetItemToCreateWithIdentity());

      var getEntry = await GetDeleteTxsAsync(HttpStatusCode.OK, MockedIdentity, policyQuoteId);
      Assert.AreEqual(expectedDeletedTxs, getEntry.Count);

      await DeleteTxsAsync(HttpStatusCode.NoContent, MockedIdentity, policyQuoteId);

      getEntry = await GetDeleteTxsAsync(HttpStatusCode.OK, MockedIdentity, policyQuoteId);
      Assert.AreEqual(0, getEntry.Count);
    }

    private string DeleteTxsUrl(UserAndIssuer userAndIssuer, long? id)
    {
      IList<(string, string)> queryParams = new List<(string, string)>();

      if (id != null)
      {
        queryParams.Add(("policyQuoteId", id.ToString()));
      }
      if (userAndIssuer?.Identity != null)
      {
        queryParams.Add(("identity", userAndIssuer.Identity));
      }
      if (userAndIssuer?.IdentityProvider != null)
      {
        queryParams.Add(("identityProvider", userAndIssuer.IdentityProvider));
      }
      return PrepareTxsUrl(queryParams);
    }

    private async Task DeleteTxsAsync(HttpStatusCode expectedStatusCode, UserAndIssuer userAndIssuer, long? id)
    {
      await Delete(Client, DeleteTxsUrl(userAndIssuer, id), expectedStatusCode);
    }

    private async Task<DeleteTxsViewModelGet> GetDeleteTxsAsync(HttpStatusCode expectedStatusCode, UserAndIssuer userAndIssuer, long? id)
    {
      return await Get<DeleteTxsViewModelGet>(Client, DeleteTxsUrl(userAndIssuer, id), expectedStatusCode);
    }


    protected virtual string PrepareTxsUrl(IList<(string, string)> queryParams)
    {
      return PrepareQueryParams(MapiServer.ApiFeeQuoteTxsUrl, queryParams);
    }
  }
}
