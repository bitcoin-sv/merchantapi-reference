// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using MerchantAPI.APIGateway.Domain.Actions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net;
using MerchantAPI.APIGateway.Test.Functional.Attributes;

namespace MerchantAPI.APIGateway.Test.Functional
{
  [TestClass]
  public class SubmitTxStatusTest : MapiTestBase
  {
    IMapi mapi;

    [TestInitialize]
    public override void TestInitialize()
    {
      base.TestInitialize();
      mapi = server.Services.GetRequiredService<IMapi>();
      var oldStatus = mapi.GetSubmitTxStatus();
      loggerTest.LogInformation("OldStatus:" + oldStatus.PrepareForLogging());
      if (oldStatus.Request == 0)
      {
        // for now it is not possible to reset prometheus counters
        AssertTxStatusEmpty();
      }
    }

    [TestCleanup]
    public override void TestCleanup()
    {
      base.TestCleanup();
    }

    public void AssertTxStatusEmpty()
    {
      var status = mapi.GetSubmitTxStatus();

      Assert.AreEqual(0, status.Request);
      Assert.AreEqual(0, status.TxAuthenticatedUser);
      Assert.AreEqual(0, status.TxAnonymousUser);
      Assert.AreEqual(0, status.Tx);
      Assert.AreEqual(0, status.AvgBatch);
      Assert.AreEqual(0, status.TxSentToNode);
      Assert.AreEqual(0, status.TxAcceptedByNode);
      Assert.AreEqual(0, status.TxRejectedByNode);
      Assert.AreEqual(0, status.TxSubmitException);
      Assert.AreEqual(0, status.TxResponseSuccess);
      Assert.AreEqual(0, status.TxResponseFailure);
      Assert.AreEqual(0, status.TxResponseException);
    }

    private async Task SubmitTransactionWithSuccessAsync(string txHex, string txHash)
    {
      var oldStatus = mapi.GetSubmitTxStatus();

      await AssertSubmitTxAsync(txHex, txHash);

      var status = mapi.GetSubmitTxStatus();
      Assert.AreEqual(oldStatus.Request + 1, status.Request);
      Assert.AreEqual(oldStatus.TxAuthenticatedUser, status.TxAuthenticatedUser);
      Assert.AreEqual(oldStatus.TxAnonymousUser + 1, status.TxAnonymousUser);
      Assert.AreEqual(oldStatus.Tx + 1, status.Tx);
      Assert.AreEqual(status.Tx / status.Request, status.AvgBatch);
      Assert.AreEqual(oldStatus.TxSentToNode + 1, status.TxSentToNode);
      Assert.AreEqual(oldStatus.TxAcceptedByNode + 1, status.TxAcceptedByNode);
      Assert.AreEqual(oldStatus.TxRejectedByNode, status.TxRejectedByNode);
      Assert.AreEqual(oldStatus.TxSubmitException, status.TxSubmitException);
      Assert.AreEqual(oldStatus.TxResponseSuccess + 1, status.TxResponseSuccess);
      Assert.AreEqual(oldStatus.TxResponseFailure, status.TxResponseFailure);
      Assert.AreEqual(oldStatus.TxResponseException, status.TxResponseException);
    }

    public async Task SubmitTransactionAndResubmitAsync(bool resubmitToNode)
    {
      await SubmitTransactionWithSuccessAsync(txC3Hex, txC3Hash);
      var oldStatus = mapi.GetSubmitTxStatus();

      await AssertSubmitTxAsync(txC3Hex, txC3Hash, expectedDescription: resubmitToNode ? "" : "Already known");
      var status = mapi.GetSubmitTxStatus();

      Assert.AreEqual(oldStatus.Request + 1, status.Request);
      Assert.AreEqual(oldStatus.TxAuthenticatedUser, status.TxAuthenticatedUser);
      Assert.AreEqual(oldStatus.TxAnonymousUser + 1, status.TxAnonymousUser);
      Assert.AreEqual(oldStatus.Tx + 1, status.Tx);
      Assert.AreEqual(status.Tx / status.Request, status.AvgBatch);
      int incSentToNode = resubmitToNode ? 1 : 0;
      Assert.AreEqual(oldStatus.TxSentToNode + incSentToNode, status.TxSentToNode);
      Assert.AreEqual(oldStatus.TxAcceptedByNode + incSentToNode, status.TxAcceptedByNode);
      Assert.AreEqual(oldStatus.TxRejectedByNode, status.TxRejectedByNode);
      Assert.AreEqual(oldStatus.TxSubmitException, status.TxSubmitException);
      Assert.AreEqual(oldStatus.TxResponseSuccess + 1, status.TxResponseSuccess);
      Assert.AreEqual(oldStatus.TxResponseFailure, status.TxResponseFailure);
      Assert.AreEqual(oldStatus.TxResponseException, status.TxResponseException);
      loggerTest.LogInformation("Status:" + status.PrepareForLogging());
    }

    [TestMethod]
    public async Task SubmitTransactionAndResubmit()
    {
      await SubmitTransactionAndResubmitAsync(false);
    }

    [OverrideSetting("AppSettings:ResubmitKnownTransactions", true)]
    [TestMethod]
    public async Task SubmitTransactionAndResubmitToNode()
    {
      await SubmitTransactionAndResubmitAsync(true);
    }

    [TestMethod]
    public async Task SubmitTransactionAuthenticatedNoFeeQuotes()
    {
      var oldStatus = mapi.GetSubmitTxStatus();
      RestAuthentication = MockedIdentityBearerAuthentication;
      await SubmitTxToMapiAsync(txC3Hex, expectedStatusCode: HttpStatusCode.InternalServerError);

      var status = mapi.GetSubmitTxStatus();
      Assert.AreEqual(oldStatus.Request + 1, status.Request);
      Assert.AreEqual(oldStatus.TxAuthenticatedUser + 1, status.TxAuthenticatedUser);
      Assert.AreEqual(oldStatus.TxAnonymousUser, status.TxAnonymousUser);
      Assert.AreEqual(oldStatus.Tx + 1, status.Tx);
      Assert.AreEqual(status.Tx / status.Request, status.AvgBatch);
      Assert.AreEqual(oldStatus.TxSentToNode, status.TxSentToNode);
      Assert.AreEqual(oldStatus.TxAcceptedByNode, status.TxAcceptedByNode);
      Assert.AreEqual(oldStatus.TxRejectedByNode, status.TxRejectedByNode);
      Assert.AreEqual(oldStatus.TxSubmitException, status.TxSubmitException);
      Assert.AreEqual(oldStatus.TxResponseSuccess, status.TxResponseSuccess);
      Assert.AreEqual(oldStatus.TxResponseFailure, status.TxResponseFailure);
      Assert.AreEqual(oldStatus.TxResponseException + 1, status.TxResponseException);
      loggerTest.LogInformation("Status:" + status.PrepareForLogging());
    }

    [TestMethod]
    public async Task SubmitTransactionsJson()
    {
      var oldStatus = mapi.GetSubmitTxStatus();
      // 2 valid, 1 too low fee (not sent to node)
      await SubmitTxsToMapiAsync(HttpStatusCode.OK);

      var status = mapi.GetSubmitTxStatus();

      Assert.AreEqual(oldStatus.Request + 1, status.Request);
      Assert.AreEqual(oldStatus.TxAuthenticatedUser, status.TxAuthenticatedUser);
      Assert.AreEqual(oldStatus.TxAnonymousUser + 3, status.TxAnonymousUser);
      Assert.AreEqual(oldStatus.Tx + 3, status.Tx);
      Assert.AreEqual(status.Tx / status.Request, status.AvgBatch);
      Assert.IsTrue(status.AvgBatch > 1);
      Assert.AreEqual(oldStatus.TxSentToNode + 2, status.TxSentToNode);
      Assert.AreEqual(oldStatus.TxAcceptedByNode + 2, status.TxAcceptedByNode);
      Assert.AreEqual(oldStatus.TxRejectedByNode, status.TxRejectedByNode);
      Assert.AreEqual(oldStatus.TxSubmitException, status.TxSubmitException);
      Assert.AreEqual(oldStatus.TxResponseSuccess + 2, status.TxResponseSuccess);
      Assert.AreEqual(oldStatus.TxResponseFailure + 1, status.TxResponseFailure);
      Assert.AreEqual(oldStatus.TxResponseException, status.TxResponseException);
      loggerTest.LogInformation("Status:" + status.PrepareForLogging());
    }
  }
}
