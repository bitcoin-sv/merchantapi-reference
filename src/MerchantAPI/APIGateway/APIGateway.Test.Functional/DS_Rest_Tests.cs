// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain.DSAccessChecks;
using MerchantAPI.APIGateway.Domain.Models;
using MerchantAPI.APIGateway.Rest.Controllers;
using MerchantAPI.APIGateway.Test.Functional.Server;
using MerchantAPI.Common.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Test.Functional
{
  [TestClass]
  public class DS_Rest_Tests : TestBase
  {
    IHostBanList banList;

    [TestInitialize]
    public void TestInitialize()
    {
      Initialize(mockedServices: true);
      banList = server.Services.GetRequiredService<IHostBanList>();
    }

    [TestCleanup]
    public void TestCleanup()
    {
      Cleanup();
    }

    private async Task InsertTXAsync()
    {
      var txList = new List<Tx>() { CreateNewTx(txC0Hash, txC0Hex, false, null, true) };
      await TxRepositoryPostgres.InsertTxsAsync(txList, false);
    }

    [TestMethod]
    public async Task InvalidQueryAsync()
    {
      var (_, httpResponse) = await GetWithHttpResponseReturned<string>(client, MapiServer.ApiDSQuery + "/a", HttpStatusCode.BadRequest);
      var responseString = await httpResponse.Content.ReadAsStringAsync();
      Assert.IsTrue(responseString.Contains("banned"));
      Assert.IsTrue(banList.IsHostBanned("localhost"));
    }

    [TestMethod]
    public async Task QueryReturnPositiveAsync()
    {
      await InsertTXAsync();
      var httpResponse = await PerformRequestAsync(client, HttpMethod.Get, MapiServer.ApiDSQuery + "/" + txC0Hash);

      Assert.AreEqual(HttpStatusCode.OK, httpResponse.StatusCode);
      Assert.IsTrue(httpResponse.Headers.Contains(DsntController.DSHeader));
      Assert.AreEqual("1", httpResponse.Headers.GetValues(DsntController.DSHeader).Single());
    }

    [TestMethod]
    public async Task QueryReturnNegativeAsync()
    {
      var httpResponse = await PerformRequestAsync(client, HttpMethod.Get, MapiServer.ApiDSQuery + "/" + txC0Hash);

      Assert.AreEqual(HttpStatusCode.OK, httpResponse.StatusCode);
      Assert.IsTrue(httpResponse.Headers.Contains(DsntController.DSHeader));
      Assert.AreEqual("0", httpResponse.Headers.GetValues(DsntController.DSHeader).Single());
    }

    [TestMethod]
    public async Task RejectSubmit4InvalidParamsAsync()
    {
      var bytes = HelperTools.HexStringToByteArray(txC0Hex);

      var reqContent = new ByteArrayContent(bytes);
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Octet);

      // We should get banned because we didn't set query parameters
      var (_, httpResponse) = await Post<string>(MapiServer.ApiDSSubmit, client, reqContent, HttpStatusCode.BadRequest);
      string responseString = await httpResponse.Content.ReadAsStringAsync();
      Assert.IsTrue(responseString.Contains("banned"));
      Assert.IsTrue(banList.IsHostBanned("localhost"));

      IList<(string, string)> queryParams = new List<(string, string)>();

      // We should get banned because we didn't set 'ctxid' query parameter
      banList.RemoveFromBanList("localhost");
      queryParams.Add(("txid", "a"));
      await Post<string>(PrepareQueryParams(MapiServer.ApiDSSubmit, queryParams), client, reqContent, HttpStatusCode.BadRequest);
      Assert.IsTrue(responseString.Contains("banned"));
      Assert.IsTrue(banList.IsHostBanned("localhost"));

      // We should get banned because we didn't set valid txId query parameter
      banList.RemoveFromBanList("localhost");
      queryParams.Add(("ctxid", "a"));
      await Post<string>(PrepareQueryParams(MapiServer.ApiDSSubmit, queryParams), client, reqContent, HttpStatusCode.BadRequest);
      Assert.IsTrue(responseString.Contains("banned"));
      Assert.IsTrue(banList.IsHostBanned("localhost"));

      // We should get banned because we didn't set valid ctxId query parameter
      banList.RemoveFromBanList("localhost");
      queryParams.Clear();
      queryParams.Add(("txid", txC0Hash));
      queryParams.Add(("ctxid", "a"));
      await Post<string>(PrepareQueryParams(MapiServer.ApiDSSubmit, queryParams), client, reqContent, HttpStatusCode.BadRequest);
      Assert.IsTrue(responseString.Contains("banned"));
      Assert.IsTrue(banList.IsHostBanned("localhost"));

      // We should not get banned because txid == ctxid, but banscore should be increased by 10
      banList.RemoveFromBanList("localhost");
      queryParams.Clear();
      queryParams.Add(("txid", txC0Hash));
      queryParams.Add(("ctxid", txC0Hash));
      await Post<string>(PrepareQueryParams(MapiServer.ApiDSSubmit, queryParams), client, reqContent, HttpStatusCode.BadRequest);
      Assert.IsFalse(banList.IsHostBanned("localhost"));
      Assert.AreEqual(banList.ReturnBanScore("localhost"), HostBanList.WarningScore);

      // We should not get banned because 'n' is not set, but banscore should be increased by 10
      banList.RemoveFromBanList("localhost");
      //await QueryReturnPositive();
      queryParams.Clear();
      queryParams.Add(("txid", txC0Hash));
      queryParams.Add(("ctxid", txC1Hash));
      await Post<string>(PrepareQueryParams(MapiServer.ApiDSSubmit, queryParams), client, reqContent, HttpStatusCode.BadRequest);
      Assert.IsFalse(banList.IsHostBanned("localhost"));
      Assert.AreEqual(banList.ReturnBanScore("localhost"), HostBanList.WarningScore);

      // We should not get banned because 'cn' is not set, but banscore should be increased by 10
      banList.RemoveFromBanList("localhost");
      queryParams.Clear();
      queryParams.Add(("txid", txC0Hash));
      queryParams.Add(("ctxid", txC1Hash));
      queryParams.Add(("n", "0"));
      await Post<string>(PrepareQueryParams(MapiServer.ApiDSSubmit, queryParams), client, reqContent, HttpStatusCode.BadRequest);
      Assert.IsFalse(banList.IsHostBanned("localhost"));
      Assert.AreEqual(banList.ReturnBanScore("localhost"), HostBanList.WarningScore);

      // We should not get banned because we didn't query for transaction, but banscore should be increased by 10
      banList.RemoveFromBanList("localhost");
      queryParams.Clear();
      queryParams.Add(("txid", txC0Hash));
      queryParams.Add(("ctxid", txC1Hash));
      queryParams.Add(("n", "0"));
      queryParams.Add(("cn", "0"));
      (_, httpResponse) = await Post<string>(PrepareQueryParams(MapiServer.ApiDSSubmit, queryParams), client, reqContent, HttpStatusCode.BadRequest);
      responseString = await httpResponse.Content.ReadAsStringAsync();
      Assert.IsTrue(responseString.Contains("banned"));
      Assert.IsTrue(banList.IsHostBanned("localhost"));
    }

    [TestMethod]
    public async Task RejectSubmitWithEmptyProofAsync()
    {
      await QueryReturnPositiveAsync();

      IList<(string, string)> queryParams = new List<(string, string)>
      {
        ("txid", txC0Hash),
        ("ctxid", txC1Hash),
        ("n", "0"),
        ("cn", "0")
      };

      var reqContent = new ByteArrayContent(new byte[] { });
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Octet);
      var response = await Post<string>(PrepareQueryParams(MapiServer.ApiDSSubmit, queryParams), client, reqContent, HttpStatusCode.BadRequest);
      var responseString = await response.httpResponse.Content.ReadAsStringAsync();
      Assert.IsTrue(responseString.Contains("banned"));
      Assert.IsTrue(banList.IsHostBanned("localhost"));
    }

    [TestMethod]
    public async Task RejectSubmitWithProofNotMatchingParameterAsync()
    {
      await QueryReturnPositiveAsync();

      IList<(string, string)> queryParams = new List<(string, string)>
      {
        ("txid", txC0Hash),
        ("ctxid", txC1Hash),
        ("n", "0"),
        ("cn", "0")
      };

      var bytes = HelperTools.HexStringToByteArray(txC0Hex);
      var reqContent = new ByteArrayContent(bytes);
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Octet);
      var response = await Post<string>(PrepareQueryParams(MapiServer.ApiDSSubmit, queryParams), client, reqContent, HttpStatusCode.BadRequest);
      var responseString = await response.httpResponse.Content.ReadAsStringAsync();
      Assert.IsTrue(responseString.Contains("banned"));
      Assert.IsTrue(banList.IsHostBanned("localhost"));
    }

    [TestMethod]
    public async Task RejectSubmitWithInvalidProofAsync()
    {
      await Nodes.CreateNodeAsync(new Node("node1", 0, "mocked", "mocked", null, null));

      await QueryReturnPositiveAsync();

      IList<(string, string)> queryParams = new List<(string, string)>
      {
        ("txid", txC0Hash),
        ("ctxid", txC1Hash),
        ("n", "0"),
        ("cn", "0")
      };

      var bytes = HelperTools.HexStringToByteArray(txC1Hex);
      var reqContent = new ByteArrayContent(bytes);
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Octet);
      var response = await Post<string>(PrepareQueryParams(MapiServer.ApiDSSubmit, queryParams), client, reqContent, HttpStatusCode.BadRequest);
      var responseString = await response.httpResponse.Content.ReadAsStringAsync();
      Assert.IsTrue(responseString.Contains("banned"));
      Assert.IsTrue(banList.IsHostBanned("localhost"));
    }

    [TestMethod]
    public async Task BanHostOnMultipleBadRequestsAsync()
    {
      Assert.AreEqual(HostBanList.BanScoreLimit, 100);
      await InsertTXAsync();

      var httpResponse1 = await PerformRequestAsync(client, HttpMethod.Get, MapiServer.ApiDSQuery + "/" + txC0Hash);
      Assert.IsFalse(banList.IsHostBanned("localhost"));
      Assert.AreEqual(HttpStatusCode.OK, httpResponse1.StatusCode);
      Assert.IsTrue(httpResponse1.Headers.Contains(DsntController.DSHeader));
      Assert.AreEqual("1", httpResponse1.Headers.GetValues(DsntController.DSHeader).Single());

      IList<(string, string)> queryParams = new List<(string, string)>
      {
        ("txid", txC0Hash),
        ("ctxid", txC0Hash)
      };
      var reqContent = new ByteArrayContent(new byte[] { });
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Octet);

      // If host calls submit with invalid missing 'n' parameter 10 times it must be banned on 10th attempt
      for (int i = 1; i <= 9; i++)
      {
        await Post<string>(PrepareQueryParams(MapiServer.ApiDSSubmit, queryParams), client, reqContent, HttpStatusCode.BadRequest);
      }

      var (_, httpResponse) = await Post<string>(PrepareQueryParams(MapiServer.ApiDSSubmit, queryParams), client, reqContent, HttpStatusCode.BadRequest);
      var responseString = await httpResponse.Content.ReadAsStringAsync();
      Assert.IsTrue(responseString.Contains("banned"));
      Assert.IsTrue(banList.IsHostBanned("localhost"));
    }

    [TestMethod]
    public async Task HostNotBannedAfterPeriodAsync()
    {
      await BanHostOnMultipleBadRequestsAsync();
      await Task.Delay(2500);

      var httpResponse = await PerformRequestAsync(client, HttpMethod.Get, MapiServer.ApiDSQuery + "/" + txC0Hash);
      Assert.IsFalse(banList.IsHostBanned("localhost"));
      Assert.AreEqual(HttpStatusCode.OK, httpResponse.StatusCode);
      Assert.IsTrue(httpResponse.Headers.Contains(DsntController.DSHeader));
      Assert.AreEqual("1", httpResponse.Headers.GetValues(DsntController.DSHeader).Single());
    }

    private async Task PerformSeriesOfInvalidTxIdQueriesAsync()
    {
      for (int i = 1; i < AppSettings.DSMaxNumOfUnknownTxQueries; i++)
      {
        await PerformRequestAsync(client, HttpMethod.Get, MapiServer.ApiDSQuery + "/" + txC0Hash);
        Assert.IsFalse(banList.IsHostBanned("localhost"));
      }
    }

    [TestMethod]
    public async Task BanHostOnMultipleInvalidTxIdQueryAsync()
    {
      Assert.AreEqual(HostBanList.BanScoreLimit, 100);

      await PerformSeriesOfInvalidTxIdQueriesAsync();

      var (_, httpResponse) = await GetWithHttpResponseReturned<string>(client, MapiServer.ApiDSQuery + "/" + txC0Hash, HttpStatusCode.BadRequest);
      var responseString = await httpResponse.Content.ReadAsStringAsync();
      Assert.IsTrue(responseString.Contains("banned"));
      Assert.IsTrue(banList.IsHostBanned("localhost"));
    }

    [TestMethod]
    public async Task DontBanHostOnMultipleInvalidTxIdQueryIfSleepPresentAsync()
    {
      Assert.AreEqual(HostBanList.BanScoreLimit, 100);

      await PerformSeriesOfInvalidTxIdQueriesAsync();

      await Task.Delay(2000);
      var httpResponse = await PerformRequestAsync(client, HttpMethod.Get, MapiServer.ApiDSQuery + "/" + txC0Hash);
      Assert.AreEqual(HttpStatusCode.OK, httpResponse.StatusCode);
      Assert.IsFalse(banList.IsHostBanned("localhost"));
    }

    private async Task PerformSeriesOfValidTxIdQueriesAsync()
    {
      await InsertTXAsync();

      for (int i = 1; i < AppSettings.DSMaxNumOfTxQueries; i++)
      {
        var httpResponse1 = await PerformRequestAsync(client, HttpMethod.Get, MapiServer.ApiDSQuery + "/" + txC0Hash);
        Assert.IsFalse(banList.IsHostBanned("localhost"));

        Assert.AreEqual(HttpStatusCode.OK, httpResponse1.StatusCode);
        Assert.IsTrue(httpResponse1.Headers.Contains(DsntController.DSHeader));
        Assert.AreEqual("1", httpResponse1.Headers.GetValues(DsntController.DSHeader).Single());
      }
    }

    [TestMethod]
    public async Task BanHostOnMultipleQueriesForSameTxIdAsync()
    {
      Assert.AreEqual(HostBanList.BanScoreLimit, 100);

      await PerformSeriesOfValidTxIdQueriesAsync();

      var (_, httpResponse) = await GetWithHttpResponseReturned<string>(client, MapiServer.ApiDSQuery + "/" + txC0Hash, HttpStatusCode.BadRequest);
      var responseString = await httpResponse.Content.ReadAsStringAsync();
      Assert.IsTrue(responseString.Contains("banned"));
      Assert.IsTrue(banList.IsHostBanned("localhost"));
    }

    [TestMethod]
    public async Task DontBanHostOnMultipleQueriesForSameTxIdIfSleepPresentAsync()
    {
      Assert.AreEqual(HostBanList.BanScoreLimit, 100);

      await PerformSeriesOfValidTxIdQueriesAsync();

      await Task.Delay(2500);
      var httpResponse = await PerformRequestAsync(client, HttpMethod.Get, MapiServer.ApiDSQuery + "/" + txC0Hash);
      Assert.AreEqual(HttpStatusCode.OK, httpResponse.StatusCode);
      Assert.IsFalse(banList.IsHostBanned("localhost"));
    }

    [TestMethod]
    public async Task RejectSubmitDoublespendTxFoNotSpendingSameInputsAsync()
    {
      await QueryReturnPositiveAsync();

      var tx1 = Transaction.Parse(txC0Hex, Network.Main);
      var tx2 = Transaction.Parse(txC0Hex, Network.Main);
      // Change version so that tx hash changes
      tx2.Inputs[0].PrevOut.N = 1;
      tx2.Check();
      IList<(string, string)> queryParams = new List<(string, string)>
      {
        ("txid", tx1.GetHash().ToString()),
        ("ctxid", tx2.GetHash().ToString()),
        ("n", "0"),
        ("cn", "0")
      };
      var reqContent = new ByteArrayContent(tx2.ToBytes());
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Octet);

      var (_, httpResponse) = await Post<string>(PrepareQueryParams(MapiServer.ApiDSSubmit, queryParams), client, reqContent, HttpStatusCode.BadRequest);
      var responseString = await httpResponse.Content.ReadAsStringAsync();
      Assert.IsTrue(responseString.Contains("banned"));
      Assert.IsTrue(banList.IsHostBanned("localhost"));
    }

    [TestMethod]
    public async Task ValidQueryAndSubmitAsync()
    {
      await QueryReturnPositiveAsync();

      var tx1 = Transaction.Parse(txC0Hex, Network.Main);
      var tx2 = Transaction.Parse(txC0Hex, Network.Main);
      // Change version so that tx hash changes
      tx2.Version = 1000;
      tx2.Check();
      IList<(string, string)> queryParams = new List<(string, string)>
      {
        ("txid", tx1.GetHash().ToString()),
        ("ctxid", tx2.GetHash().ToString()),
        ("n", "0"),
        ("cn", "0")
      };
      var reqContent = new ByteArrayContent(tx2.ToBytes());
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Octet);

      var mockNode = new Node(0, "mocked", 0, "mocked", "mocked", "This is a mock node", null, (int)NodeStatus.Connected, null, null);
      _ = Nodes.CreateNodeAsync(mockNode).Result;

      rpcClientFactoryMock.AddScriptCombination(tx2.ToHex(), (int)tx2.Inputs[0].PrevOut.N);
      _ = rpcClientFactoryMock.Create("mocked", 0, "mocked", "mocked");

      var response = await PerformRequestAsync(client, HttpMethod.Post, PrepareQueryParams(MapiServer.ApiDSSubmit, queryParams), reqContent);
      Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
      var responseString = await response.Content.ReadAsStringAsync();
      Assert.IsFalse(responseString.Contains("banned"));
      Assert.IsFalse(banList.IsHostBanned("localhost"));
    }
  }
}
