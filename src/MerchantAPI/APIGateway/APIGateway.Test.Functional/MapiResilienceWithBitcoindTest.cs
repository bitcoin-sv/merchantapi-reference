// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain;
using MerchantAPI.APIGateway.Domain.Actions;
using MerchantAPI.APIGateway.Domain.Models;
using MerchantAPI.APIGateway.Test.Functional.Attributes;
using MerchantAPI.APIGateway.Test.Functional.Mock;
using MerchantAPI.APIGateway.Test.Functional.Server;
using MerchantAPI.Common.Json;
using MerchantAPI.Common.Test.Clock;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Test.Functional
{
  [TestClass]
  public class MapiResilienceWithBitcoindTest : MapiWithBitcoindTestBase
  {
    MapiMock mapiMock;

    [TestInitialize]
    public override void TestInitialize()
    {
      base.TestInitialize();
      InsertFeeQuote();
      mapiMock = server.Services.GetRequiredService<IMapi>() as MapiMock;
    }


    [TestCleanup]
    public override void TestCleanup()
    {
      base.TestCleanup();
    }

    public override TestServer CreateServer(bool mockedServices, TestServer serverCallback, string dbConnectionString, IEnumerable<KeyValuePair<string, string>> overridenSettings = null)
    {
      // special - we need mAPI with different modes
      return new TestServerBase(DbConnectionStringDDL).CreateServer<MapiServer, APIGatewayTestsMockStartup, APIGatewayTestsStartupMapiMock>(mockedServices, serverCallback, dbConnectionString, overridenSettings);
    }

    [TestMethod]
    public async Task SubmitSingleTxAndCheckTxStatus()
    {
      // Create transaction  and submit it to the first node
      var (txHex, txHash) = CreateNewTransaction();
      mapiMock.SimulateMode(Faults.SimulateSendTxsResponse.NodeFailsWhenSendRawTxs);

      var payloadSubmit = await SubmitTransactionAsync(txHex);
      Assert.AreEqual("failure", payloadSubmit.ReturnResult);
      await AssertTxStatus(txHash, TxStatus.NotPresentInDb);

      mapiMock.ClearMode();
      payloadSubmit = await SubmitTransactionAsync(txHex);
      Assert.AreEqual("success", payloadSubmit.ReturnResult);
      await AssertTxStatus(txHash, TxStatus.Mempool);
    }

    [DataRow(3)]
    [TestMethod]
    public async Task SubmitTransactionsDifferentUsers(int nTxs)
    {
      // unauthorized user has limited maxscriptsizepolicy
      SetPoliciesForCurrentFeeQuote("{\"maxscriptsizepolicy\": 105 }");
      // insert new feeQuote for authorized user (without policies)
      InsertFeeQuote(MockedIdentity);
      List<string> txHexList = new();
      // test single update and batch
      for (int i = 0; i < nTxs; i++)
      {
        (string txHex, string _) = CreateNewTransaction();
        txHexList.Add(txHex);
      }

      var payloadSubmit = await SubmitTransactionsAsync(txHexList.ToArray());
      Assert.IsTrue(payloadSubmit.Txs.All(x => x.ReturnResult == "failure"));

      RestAuthentication = MockedIdentityBearerAuthentication;
      payloadSubmit = await SubmitTransactionsAsync(txHexList.ToArray());
      Assert.IsTrue(payloadSubmit.Txs.All(x => x.ReturnResult == "success"));
    }

    [DataRow(false)]
    [DataRow(true)]
    [TestMethod]
    public async Task DeleteTxsWithInvalidPolicyQuote(bool authenticated)
    {
      var identity = new Common.Authentication.UserAndIssuer();
      if (authenticated)
      {
        InsertFeeQuote(MockedIdentity);
        RestAuthentication = MockedIdentityBearerAuthentication;
        identity = MockedIdentity;
      }
      SetPoliciesForCurrentFeeQuote("{\"invalidpolicy\": 105 }", identity);

      (string txHex, string _) = CreateNewTransaction();

      var payloadSubmit = await SubmitTransactionAsync(txHex);
      Assert.AreEqual("failure", payloadSubmit.ReturnResult);

      mapiMock.SimulateMode(Faults.SimulateSendTxsResponse.NodeFailsWhenSendRawTxs);
      payloadSubmit = await SubmitTransactionAsync(txHex);
      Assert.AreEqual("failure", payloadSubmit.ReturnResult);

      mapiMock.ClearMode();
      SetPoliciesForCurrentFeeQuote("{\"maxscriptsizepolicy\": 1000 }", identity, emptyFeeQuoteRepo: false);

      using (MockedClock.NowIs(DateTime.UtcNow.AddMinutes(this.AppSettings.QuoteExpiryMinutes.Value)))
      {
        if (!authenticated)
        {
          // for anonymous users policy is not saved
          payloadSubmit = await SubmitTransactionAsync(txHex);
          Assert.AreEqual("success", payloadSubmit.ReturnResult);
        }
        else
        {
          // we first resubmit with saved invalid policy
          payloadSubmit = await SubmitTransactionAsync(txHex);
          Assert.AreEqual("failure", payloadSubmit.ReturnResult);

          /*
          var feequote = FeeQuoteRepository.GetFeeQuoteById(1);
          int deleted = await TxRepositoryPostgres.DeleteTxsWithFeeQuotesAsync(new FeeQuote[] { feequote });
          Assert.AreEqual(1, deleted);
          // resubmit with new (fixed) policy
          payloadSubmit = await SubmitTransactionAsync(txHex);
          Assert.AreEqual("success", payloadSubmit.ReturnResult);*/
        }
      }
    }

    [TestMethod]
    public async Task ResubmitTxWithSavedPolicy()
    {
      RestAuthentication = MockedIdentityBearerAuthentication;
      string policies = "{\"maxscriptsizepolicy\":106}";
      string policiesTooLow = "{\"maxscriptsizepolicy\":-1}";
      (var txHex, _) = CreateNewTransaction();

      InsertFeeQuote(MockedIdentity);
      SetPoliciesForCurrentFeeQuote(policies, MockedIdentity, emptyFeeQuoteRepo: true);

      mapiMock.SimulateDbFault(Faults.FaultType.DbBeforeSavingUncommittedState, Faults.DbFaultComponent.MapiAfterSendToNode);

      var payloadSubmit = await SubmitTransactionAsync(txHex, expectedHttpStatusCode: System.Net.HttpStatusCode.InternalServerError);
      Assert.IsNull(payloadSubmit);

      // tx1 submit with policies is successful
      (var tx1Hex, _) = CreateNewTransaction();
      mapiMock.ClearMode();
      payloadSubmit = await SubmitTransactionAsync(tx1Hex);
      Assert.AreEqual("success", payloadSubmit.ReturnResult);

      using (MockedClock.NowIs(DateTime.UtcNow.AddMinutes(AppSettings.QuoteExpiryMinutes.Value)))
      {
        SetPoliciesForCurrentFeeQuote(policiesTooLow, MockedIdentity, emptyFeeQuoteRepo: false);
        // tx2 submit with policiesTooLow is unsuccessful
        (var tx2Hex, _) = CreateNewTransaction();
        var failedSubmit = await SubmitTransactionAsync(tx2Hex);
        Assert.AreEqual("failure", failedSubmit.ReturnResult);

        // tx submit with saved policies is successful
        payloadSubmit = await SubmitTransactionAsync(txHex);
        Assert.AreEqual("success", payloadSubmit.ReturnResult);
      }
    }

    [TestMethod]
    public async Task ResubmitSameTransactionMultipleTimesAsync()
    {
      var (txHex1, _) = CreateNewTransaction();

      // Submit tx to node two times. Re-submitting same tx should get same response.
      // If first submit succeeds, also second submit must succeed. 
      // (in mAPI versions before 1.5 error "Transaction already in the mempool" was returned)
      var tx1_payload1 = await SubmitTransactionAsync(txHex1);
      var tx1_payload2 = await SubmitTransactionAsync(txHex1);

      Assert.AreEqual("success", tx1_payload1.ReturnResult);
      Assert.AreEqual("success", tx1_payload2.ReturnResult);
      Assert.AreEqual("Already known", tx1_payload2.ResultDescription);
    }

    private async Task ResubmitKnownTransactionsMultipleTimesAsync(bool resubmitToNode, int txsInBatch)
    {
      //make additional coins
      foreach (var coin in GetCoins(base.rpcClient0, txsInBatch * TxStatus.MapiSuccessTxStatuses.Length))
      {
        availableCoins.Enqueue(coin);
      }

      List<Tx> processed = new();
      foreach (var txStatus in TxStatus.MapiSuccessTxStatuses)
      {
        loggerTest.LogInformation($"Processing txStatus: {txStatus}");
        List<Tx> txToInsert = new();
        // Store txs to database before submitting it to the mAPI
        // (so that we can test all possible txStatuses)
        for (int i = 0; i < txsInBatch; i++)
        {
          var (txHex, txId) = CreateNewTransaction();
          txToInsert.Add(CreateNewTx(txId, txHex, false, null, false, txStatus));
        } 
        await TxRepositoryPostgres.InsertOrUpdateTxsAsync(txToInsert, false);

        txToInsert.ForEach(async x => x.SubmittedAt = (await TxRepositoryPostgres.GetTransactionAsync(x.TxExternalId.ToBytes())).SubmittedAt);

        // Re-submitting same txs twice should return same response (success).
        var response = await SubmitTransactionsAsync(txToInsert.Select(x => HelperTools.ByteToHexString(x.TxPayload)).ToArray());
        Assert.AreEqual(0L, response.FailureCount);
        Assert.IsTrue(response.Txs.All(x => x.ReturnResult == "success"));

        // when submitted to node, submittedAt should change, txStatus should remain same
        foreach (var t in txToInsert)
        {
          var dbTx = await TxRepositoryPostgres.GetTransactionAsync(t.TxExternalIdBytes);
          if (!resubmitToNode)
          {
            Assert.AreEqual(t.SubmittedAt.Ticks, dbTx.SubmittedAt.Ticks, 10);
          }
          else
          {
            Assert.AreNotEqual(t.SubmittedAt, dbTx.SubmittedAt);
          }
          Assert.AreEqual(t.TxStatus, dbTx.TxStatus);
        }

        // previously inserted txs must stil have their txstatus
        foreach(var t in processed)
        {
          var dbTx = await TxRepositoryPostgres.GetTransactionAsync(t.TxExternalIdBytes);
          Assert.AreEqual(t.TxStatus, dbTx.TxStatus);
        }
        processed.AddRange(txToInsert);
      }
    }

    [TestMethod]
    public async Task ResubmitKnownTransactionsMultipleTimesAsync()
    {
      // test single update and batch
      await ResubmitKnownTransactionsMultipleTimesAsync(false, 1);
      await ResubmitKnownTransactionsMultipleTimesAsync(false, 3);
    }

    [TestMethod]
    [OverrideSetting("AppSettings:ResubmitKnownTransactions", true)]
    public async Task ResubmitKnownTransactionsToNodeMultipleTimesAsync()
    {
      // test single update and batch
      await ResubmitKnownTransactionsMultipleTimesAsync(true, 1);
      await ResubmitKnownTransactionsMultipleTimesAsync(true, 3);
    }

    [TestMethod]
    public async Task SubmitSameTransactionInParallelAsync()
    {
      // we want to simulate situation, where the same tx is sent in parallel in two different submits
      // we send tx directly to node and save tx to database with 'notInDb' status
      // in this way we pass the 'already in db' check
      // in this test we check the bitcoind error handling (all combinations regarding mAPI are tested in MapiResilienceTest)
      using CancellationTokenSource cts = new(cancellationTimeout);

      var (txHex1, txId1) = CreateNewTransaction();
      var tx1_result1 = await node0.RpcClient.SendRawTransactionAsync(HelperTools.HexStringToByteArray(txHex1), true, false, cts.Token);
      Assert.AreEqual(txId1, tx1_result1);

      // Store tx to database before submitting it to the mAPI
      List<Domain.Models.Tx> txToInsert = new()
      {
        new Domain.Models.Tx()
        {
          TxPayload = HelperTools.HexStringToByteArray(txHex1),
          TxExternalId = new uint256(txId1),
          ReceivedAt = DateTime.UtcNow,
          MerkleProof = false,
          DSCheck = false,
          TxStatus = TxStatus.NotPresentInDb
        }
      };
      await TxRepositoryPostgres.InsertOrUpdateTxsAsync(txToInsert, false);

      // Re-submitting same tx should get same response (success).
      var tx1_payload1 = await SubmitTransactionAsync(txHex1);
      Assert.AreEqual("success", tx1_payload1.ReturnResult);

      await AssertTxStatus(txId1, TxStatus.NotPresentInDb);
    }
  }
}
