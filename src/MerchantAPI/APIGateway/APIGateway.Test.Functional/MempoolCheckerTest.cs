// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain.Actions;
using MerchantAPI.APIGateway.Domain.ViewModels;
using MerchantAPI.APIGateway.Test.Functional.Attributes;
using MerchantAPI.APIGateway.Test.Functional.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Test.Functional
{
  [TestClass]
  public class MempoolCheckerTest: MapiTestBase
  {
    // RpcClientMock correctly mocks simple situation (as is the addition of the tx to mempool on submit) 
    // but does not cover correctly removal of txs, mininig blocks, reorgs ...
    // so most of the tests must be executed with bitcoind

    IMempoolChecker mempoolChecker;

    [TestInitialize]
    public override void TestInitialize()
    {
      base.TestInitialize();
      mempoolChecker = server.Services.GetRequiredService<IMempoolChecker>();
      
      PublishBlockHashToEventBus(RpcMultiClient.GetBestBlockchainInfoAsync().Result.BestBlockHash);
      WaitUntilEventBusIsIdle();
    }

    [TestCleanup]
    public override void TestCleanup()
    {
      base.TestCleanup();
    }

    [TestMethod]
    [OverrideSetting("AppSettings:MempoolCheckerEnabled", false)]
    public void CheckConfigResubmitKnownTransactions()
    {
      Assert.IsFalse(mempoolChecker.ExecuteCheckMempoolAndResubmitTxs);
    }

    [TestMethod]
    [OverrideSetting("AppSettings:DontParseBlocks", true)]
    public async Task CheckConfigDontParseBlocks()
    {
      var info = await BlockChainInfo.GetInfoAsync();
      Assert.IsNotNull(info.BestBlockHash);
      var blockParser = server.Services.GetRequiredService<IBlockParser>();
      Assert.IsNull(blockParser.GetBlockParserStatus().LastBlockHash);
      Assert.IsFalse(mempoolChecker.ExecuteCheckMempoolAndResubmitTxs);
    }

    [TestMethod]
    public async Task CheckEmptyTxTable()
    {
      var mempoolTxs = await RpcMultiClient.GetRawMempool();
      Assert.AreEqual(0, mempoolTxs.Length);

      var txs = await TxRepositoryPostgres.GetMissingTransactionsAsync(mempoolTxs);
      Assert.AreEqual(0, txs.Length);

      bool success = await mempoolChecker.CheckMempoolAndResubmitTxs(0);
      Assert.IsTrue(success);
    }

    private async Task<(SignedPayloadViewModel response, HttpResponseMessage httpResponse)> SubmitTxToMapi(string txHex, HttpStatusCode expectedStatusCode)
    {
      var reqContent = new StringContent($"{{ \"rawtx\": \"{txHex}\" }}");
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);

      return await Post<SignedPayloadViewModel>(MapiServer.ApiMapiSubmitTransaction, Client, reqContent, expectedStatusCode);
    }
  }
}
