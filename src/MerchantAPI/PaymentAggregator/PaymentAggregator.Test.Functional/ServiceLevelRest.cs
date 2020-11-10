// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.Common.Test;
using MerchantAPI.PaymentAggregator.Consts;
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
using System.Threading.Tasks;

namespace MerchantAPI.PaymentAggregator.Test.Functional
{
  [TestClass]
  public class ServiceLevelRest : CommonRestMethodsBase<ServiceLevelArrayViewModelGet, ServiceLevelArrayViewModelCreate, AppSettings>
  {
    public override string LOG_CATEGORY { get { return "MerchantAPI.PaymentAggregator.Test.Functional"; } }
    public override string DbConnectionString { get { return Configuration["PaymentAggregatorConnectionStrings:DBConnectionString"]; } }

    public override TestServer CreateServer(bool mockedServices, TestServer serverCallback, string dbConnectionString)
    {
      return new TestServerBase().CreateServer<MapiServer, PaymentAggregatorTestsStartup, MerchantAPI.PaymentAggregator.Rest.Startup>(mockedServices, serverCallback, dbConnectionString);
    }

    public ServiceLevelRepositoryPostgres ServiceLevelRepository { get; private set; }

    [TestInitialize]
    public void TestInitialize()
    {
      Initialize(mockedServices: true);
      ApiKeyAuthentication = AppSettings.RestAdminAPIKey;
      ServiceLevelRepository = server.Services.GetRequiredService<IServiceLevelRepository>() as ServiceLevelRepositoryPostgres;
    }

    [TestCleanup]
    public void TestCleanup()
    {
      Cleanup();
    }


    public override string GetNonExistentKey() => "-1";
    public override string GetBaseUrl() => MapiServer.ApiServiceLevelUrl;
    public override string ExtractGetKey(ServiceLevelArrayViewModelGet entry) => "";// VM has no id
    public override string ExtractPostKey(ServiceLevelArrayViewModelCreate entry) => "";// VM has no id

    public override void SetPostKey(ServiceLevelArrayViewModelCreate entry, string key)
    {
      return; 
    }

    private static ServiceLevelFeeViewModelCreate CreateServiceLevelStandardFee(int level)
    {
      return new ServiceLevelFeeViewModelCreate()
      {
        FeeType = Const.FeeType.Standard,
        ServiceLevelMiningFeeAmount = new ServiceLevelFeeAmountViewModelCreate
        {
          Satoshis = 200 + (100 * level),
          Bytes = 1000
        },
        ServiceLevelRelayFeeAmount = new ServiceLevelFeeAmountViewModelCreate
        {
          Satoshis = 220 + (100 * level),
          Bytes = 1000
        },
      };
    }

    private static ServiceLevelFeeViewModelCreate CreateServiceLevelDataFee(int level)
    {
      return new ServiceLevelFeeViewModelCreate()
      {
        FeeType = Const.FeeType.Data,
        ServiceLevelMiningFeeAmount = new ServiceLevelFeeAmountViewModelCreate
        {
          Satoshis = 100 + (100 * level),
          Bytes = 1000
        },
        ServiceLevelRelayFeeAmount = new ServiceLevelFeeAmountViewModelCreate
        {
          Satoshis = 110 + (100 * level),
          Bytes = 1000
        },
      };
    }

    public static ServiceLevelArrayViewModelCreate GetServiceLevelArrayViewModelCreate()
    {
      return new ServiceLevelArrayViewModelCreate
      {
        ServiceLevels = new[]
  {
          new ServiceLevelViewModelCreate()
          {
            ServiceLevelId = 1,
            Level = 0,
            Description = "No miner will mine",
            Fees = new[]
            {
              CreateServiceLevelStandardFee(0),
              CreateServiceLevelDataFee(0)
            }
          },
          new ServiceLevelViewModelCreate()
          {
            ServiceLevelId = 2,
            Level = 1,
            Description = "Slow to mine",
            Fees = new[]
            {
              CreateServiceLevelStandardFee(1),
              CreateServiceLevelDataFee(1)
            }
          },
          new ServiceLevelViewModelCreate()
          {
            ServiceLevelId = 3,
            Level = 2,
            Description = "Fast to mine",
            Fees = null
          }
        }
      };
    }
    public override ServiceLevelArrayViewModelCreate GetItemToCreate()
    {
      return GetServiceLevelArrayViewModelCreate();
    }

    public override ServiceLevelArrayViewModelCreate[] GetItemsToCreate()
    {
      return new[] {
        new ServiceLevelArrayViewModelCreate
        {
          ServiceLevels = new[]
          {
            new ServiceLevelViewModelCreate()
            {
              ServiceLevelId = 1,
              Level = 0,
              Description = "Very slow to mine",
              Fees = new[]
              {
                CreateServiceLevelStandardFee(0),
                CreateServiceLevelDataFee(0)
              }
            },
            new ServiceLevelViewModelCreate()
            {
              ServiceLevelId = 2,
              Level = 1,
              Description = "Very fast to mine",
              Fees = null
            }
          }
        },
        new ServiceLevelArrayViewModelCreate
        {
          ServiceLevels = new[]
          {
            new ServiceLevelViewModelCreate()
            {
              ServiceLevelId = 3,
              Level = 0,
              Description = "No miner will mine",
              Fees = new[]
              {
                CreateServiceLevelStandardFee(0),
                CreateServiceLevelDataFee(0)
              }
            },
            new ServiceLevelViewModelCreate()
            {
              ServiceLevelId = 4,
              Level = 1,
              Description = "Slow to mine",
              Fees = new[]
              {
                CreateServiceLevelStandardFee(1),
                CreateServiceLevelDataFee(1)
              }
            },
            new ServiceLevelViewModelCreate()
            {
              ServiceLevelId = 5,
              Level = 2,
              Description = "Fast to mine",
              Fees = null
            }
          }
        }
      };
    }

    public override void ModifyEntry(ServiceLevelArrayViewModelCreate entry)
    {
      // nothing can be changed
    }

    public override void CheckWasCreatedFrom(ServiceLevelArrayViewModelCreate post, ServiceLevelArrayViewModelGet get)
    {
      Assert.AreEqual(post.ServiceLevels.Length, get.ServiceLevels.Length);
      foreach(var postEntry in post.ServiceLevels)
      {
        var getEntry = get.ServiceLevels.Single(x => x.Level == postEntry.Level); 
        Assert.IsNull(getEntry.ValidTo); // we should always return active service levels
        if (postEntry.Fees == null)
        {
          Assert.IsNull(getEntry.Fees);
        }
        else
        {
          for (int i = 0; i < postEntry.Fees.Length; i++)
          {
            var postFee = postEntry.Fees[i].ToDomainObject();
            var getFee = getEntry.Fees.Single(x => x.FeeType == postFee.FeeType);
            Assert.AreEqual(postFee.FeeType, getFee.FeeType);
            Assert.AreEqual(postFee.MiningFee.Bytes, getFee.MiningFee.ToDomainObject(Const.AmountType.MiningFee).Bytes);
            Assert.AreEqual(postFee.MiningFee.Satoshis, getFee.MiningFee.ToDomainObject(Const.AmountType.MiningFee).Satoshis);
            Assert.AreEqual(postFee.RelayFee.Bytes, getFee.RelayFee.ToDomainObject(Const.AmountType.RelayFee).Bytes);
            Assert.AreEqual(postFee.RelayFee.Satoshis, getFee.RelayFee.ToDomainObject(Const.AmountType.RelayFee).Satoshis);
          }
        }
      }
    }

    public override string UrlForKey(string key)
    {
      return GetBaseUrl(); // special case - we are only interested in active ServiceLevels
    }

    [TestMethod]
    public override async Task GetCollection_NoElements_ShouldReturn200Empty()
    {
      // we do not support GET multiple, only return single
      // if there is no active service levels, we return NotFound
      var httpResponse = await PerformRequestAsync(client, HttpMethod.Get, GetBaseUrl());
      Assert.AreEqual(HttpStatusCode.NotFound, httpResponse.StatusCode);
    }

    [TestMethod]
    public override async Task Put()
    {
      var entryPost = GetItemToCreate();
      var entryPostKey = ExtractPostKey(entryPost);

      // Check that id does not exists (database is deleted at start of test)
      await Get<ServiceLevelArrayViewModelGet>(client, UrlForKey(entryPostKey), HttpStatusCode.NotFound);

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
        await Post<ServiceLevelArrayViewModelCreate, ServiceLevelArrayViewModelGet>(client, entry, HttpStatusCode.Created);
      }

      // Delete first one - we do not support delete action
      await Delete(client, GetBaseUrl(), HttpStatusCode.MethodNotAllowed);

    }

    [Ignore]
    public override Task Delete_NoElement_ShouldReturnNoContent()
    {
      return base.Delete_NoElement_ShouldReturnNoContent();
    }

    [TestMethod]
    public override async Task GetMultiple()
    {
      var entries = GetItemsToCreate();

      foreach (var entry in entries)
      {
        // Create new one using POST
        await Post<ServiceLevelArrayViewModelCreate, ServiceLevelArrayViewModelGet>(client, entry, HttpStatusCode.Created);
      }

      // We should retrieve last:
      var getEntries = await Get<ServiceLevelArrayViewModelGet>(client, GetBaseUrl(), HttpStatusCode.OK);
      CheckWasCreatedFrom(entries.Last(),getEntries);
    }

    [TestMethod]
    public override async Task MultiplePost()
    {
      var entryPost = GetItemToCreate();

      var entryPostKey = ExtractPostKey(entryPost);

      // Check that no active service levels exists (database is deleted at start of test)
      await Get<ServiceLevelArrayViewModelGet>(client, UrlForKey(entryPostKey), HttpStatusCode.NotFound);

      // Create new ones using POST
      await Post<ServiceLevelArrayViewModelCreate, ServiceLevelArrayViewModelGet>(client, entryPost, HttpStatusCode.Created);

      // Try to create it again - it will not fail, because previously active service levels are deactivated 
      await Post<ServiceLevelArrayViewModelCreate, ServiceLevelArrayViewModelGet>(client, entryPost, HttpStatusCode.Created);
    }

    [TestMethod]
    public override async Task TestPost_2x_ShouldReturn409()
    {
      var entryPost = GetItemToCreate();

      await Post<ServiceLevelArrayViewModelCreate, ServiceLevelArrayViewModelGet>(client, entryPost, HttpStatusCode.Created);
      (var post2, _) = await Post<ServiceLevelArrayViewModelCreate, ServiceLevelArrayViewModelGet>(client, entryPost, HttpStatusCode.Created);

      // does not fail with conflict, but we should get second Post entry as active (which has higher SL id's)
      var result = await Get<ServiceLevelArrayViewModelGet>(client, UrlForKey(null), HttpStatusCode.OK);
      CheckWasCreatedFrom(entryPost, result);

      // we cannot know from the REST result if we get the new one (we do not return ids), so we check directly on db
      var domainSLs = ServiceLevelRepository.GetServiceLevels().OrderBy(x => x.Level).ToArray();
      var orderedPost2 = post2.ServiceLevels.OrderBy(x => x.Level).ToArray();
      var c = entryPost.ServiceLevels.Length;
      for (int i = 0; i < entryPost.ServiceLevels.Length; i++)
      {
        Assert.AreEqual(entryPost.ServiceLevels[i].ServiceLevelId + c, domainSLs[i].ServiceLevelId);
      }
    }

    [TestMethod]
    public async Task PostServiceLevelWithUnorderedLevels()
    {
      var entryPost = GetItemToCreate();
      var tmp = entryPost.ServiceLevels[0];
      entryPost.ServiceLevels[0] = entryPost.ServiceLevels[1];
      entryPost.ServiceLevels[1] = tmp;
      await Post<ServiceLevelArrayViewModelCreate, ServiceLevelArrayViewModelGet>(client, entryPost, HttpStatusCode.Created);

      var result = await Get<ServiceLevelArrayViewModelGet>(client, UrlForKey(null), HttpStatusCode.OK);
      CheckWasCreatedFrom(entryPost, result);
      // returned two levels should now increment
      Assert.AreEqual(result.ServiceLevels[0].Level, 0);
      Assert.AreEqual(result.ServiceLevels[1].Level, 1);
    }

    [TestMethod]
    public async Task InvalidPostServiceLevel()
    {
      // Try to create it - it should fail
      var entryPost = GetItemToCreate();
      entryPost.ServiceLevels = null;
      await Post<ServiceLevelArrayViewModelCreate, ServiceLevelArrayViewModelGet>(client, entryPost, HttpStatusCode.BadRequest);

      // test serviceLevel
      entryPost = GetItemToCreate();
      entryPost.ServiceLevels[0] = null;
      await Post<ServiceLevelArrayViewModelCreate, ServiceLevelArrayViewModelGet>(client, entryPost, HttpStatusCode.BadRequest);
      entryPost = GetItemToCreate();
      entryPost.ServiceLevels[0].Description = null;
      await Post<ServiceLevelArrayViewModelCreate, ServiceLevelArrayViewModelGet>(client, entryPost, HttpStatusCode.BadRequest);
      entryPost = GetItemToCreate();
      entryPost.ServiceLevels[0].Level = -1;
      await Post<ServiceLevelArrayViewModelCreate, ServiceLevelArrayViewModelGet>(client, entryPost, HttpStatusCode.BadRequest);
      // test duplicate levels
      entryPost = GetItemToCreate();
      entryPost.ServiceLevels[0].Level = entryPost.ServiceLevels[1].Level;
      await Post<ServiceLevelArrayViewModelCreate, ServiceLevelArrayViewModelGet>(client, entryPost, HttpStatusCode.BadRequest);
      // test levels must increment for 1
      entryPost = GetItemToCreate();
      entryPost.ServiceLevels[1].Level++;
      entryPost.ServiceLevels[2].Level++;
      await Post<ServiceLevelArrayViewModelCreate, ServiceLevelArrayViewModelGet>(client, entryPost, HttpStatusCode.BadRequest);

      // test fee
      entryPost = GetItemToCreate();
      entryPost.ServiceLevels.First().Fees = new ServiceLevelFeeViewModelCreate[0];
      await Post<ServiceLevelArrayViewModelCreate, ServiceLevelArrayViewModelGet>(client, entryPost, HttpStatusCode.BadRequest);
      // test null allowed only on last level
      entryPost = GetItemToCreate();
      entryPost.ServiceLevels.First().Fees = null;
      await Post<ServiceLevelArrayViewModelCreate, ServiceLevelArrayViewModelGet>(client, entryPost, HttpStatusCode.BadRequest);
      entryPost = GetItemToCreate();
      entryPost.ServiceLevels.Last().Fees = null;
      await Post<ServiceLevelArrayViewModelCreate, ServiceLevelArrayViewModelGet>(client, entryPost, HttpStatusCode.Created);
    }

    [TestMethod]
    public async Task InvalidPostFees()
    {
      // Try to create it - it should fail
      var entryPost = GetItemToCreate();
      entryPost.ServiceLevels.First().Fees[0] = null;
      await Post<ServiceLevelArrayViewModelCreate, ServiceLevelArrayViewModelGet>(client, entryPost, HttpStatusCode.BadRequest);

      // test feeTypes
      entryPost = GetItemToCreate();
      entryPost.ServiceLevels.First().Fees[0].FeeType = null;
      await Post<ServiceLevelArrayViewModelCreate, ServiceLevelArrayViewModelGet>(client, entryPost, HttpStatusCode.BadRequest);
      // test duplicate feeTypes
      entryPost = GetItemToCreate();
      entryPost.ServiceLevels.First().Fees[0].FeeType = entryPost.ServiceLevels.First().Fees[1].FeeType;
      await Post<ServiceLevelArrayViewModelCreate, ServiceLevelArrayViewModelGet>(client, entryPost, HttpStatusCode.BadRequest);
      // test feeTypes different on one level
      entryPost = GetItemToCreate();
      entryPost.ServiceLevels.First().Fees[0].FeeType = "other";
      await Post<ServiceLevelArrayViewModelCreate, ServiceLevelArrayViewModelGet>(client, entryPost, HttpStatusCode.BadRequest);
      // test feeType case sensitivity
      entryPost = GetItemToCreate();
      entryPost.ServiceLevels.First().Fees[0].FeeType = Const.FeeType.Standard.ToUpper();
      await Post<ServiceLevelArrayViewModelCreate, ServiceLevelArrayViewModelGet>(client, entryPost, HttpStatusCode.BadRequest);

      // test feeAmounts
      entryPost = GetItemToCreate();
      entryPost.ServiceLevels.First().Fees[0].ServiceLevelMiningFeeAmount = null;
      await Post<ServiceLevelArrayViewModelCreate, ServiceLevelArrayViewModelGet>(client, entryPost, HttpStatusCode.BadRequest);

      entryPost = GetItemToCreate();
      entryPost.ServiceLevels.First().Fees[0].ServiceLevelMiningFeeAmount.Satoshis = -1;
      await Post<ServiceLevelArrayViewModelCreate, ServiceLevelArrayViewModelGet>(client, entryPost, HttpStatusCode.BadRequest);

      entryPost = GetItemToCreate();
      entryPost.ServiceLevels.First().Fees[0].ServiceLevelRelayFeeAmount.Bytes = -1;
      await Post<ServiceLevelArrayViewModelCreate, ServiceLevelArrayViewModelGet>(client, entryPost, HttpStatusCode.BadRequest);

      entryPost = GetItemToCreate();
      entryPost.ServiceLevels.First().Fees[1].FeeType = entryPost.ServiceLevels.First().Fees[0].FeeType;
      await Post<ServiceLevelArrayViewModelCreate, ServiceLevelArrayViewModelGet>(client, entryPost, HttpStatusCode.BadRequest);

      // miningFees must increment with level 
      entryPost = GetItemToCreate();
      entryPost.ServiceLevels[1].Fees[1].ServiceLevelMiningFeeAmount.Satoshis -= 200;
      await Post<ServiceLevelArrayViewModelCreate, ServiceLevelArrayViewModelGet>(client, entryPost, HttpStatusCode.BadRequest);
      // relayFees must increment with level 
      entryPost = GetItemToCreate();
      entryPost.ServiceLevels[1].Fees[1].ServiceLevelRelayFeeAmount.Satoshis -= 200;
      await Post<ServiceLevelArrayViewModelCreate, ServiceLevelArrayViewModelGet>(client, entryPost, HttpStatusCode.BadRequest);
      // check equal
      entryPost = GetItemToCreate();
      entryPost.ServiceLevels[1].Fees[1].ServiceLevelRelayFeeAmount.Satoshis = entryPost.ServiceLevels[0].Fees[1].ServiceLevelRelayFeeAmount.Satoshis;
      await Post<ServiceLevelArrayViewModelCreate, ServiceLevelArrayViewModelGet>(client, entryPost, HttpStatusCode.BadRequest);
    }
  }
}
