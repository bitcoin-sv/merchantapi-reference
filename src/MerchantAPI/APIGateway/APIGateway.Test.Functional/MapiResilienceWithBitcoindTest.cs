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
  [TestCategory("TestCategoryNo3")]
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

      await SubmitTransactionAsync(txHex, expectedHttpStatusCode: System.Net.HttpStatusCode.InternalServerError);
      await AssertTxStatus(txHash, TxStatus.NotPresentInDb);

      mapiMock.ClearMode();
      var payloadSubmit = await SubmitTransactionAsync(txHex);
      Assert.AreEqual("success", payloadSubmit.ReturnResult);
      await AssertTxStatus(txHash, TxStatus.Accepted);
    }

    [TestMethod]
    public async Task SubmitSingleTxAndCheckNodeRejectedStatus()
    {
      // Create transaction and submit it to the node
      var (txHex, txHash) = CreateNewTransaction();
      SetPoliciesForCurrentFeeQuote("{\"invalidpolicy\": 0 }");

      var payloadSubmit = await SubmitTransactionAsync(txHex);
      Assert.AreEqual("failure", payloadSubmit.ReturnResult);
      await AssertTxStatus(txHash, TxStatus.NotPresentInDb);

      (txHex, txHash) = CreateNewTransaction();
      InsertFeeQuote(MockedIdentity);
      SetPoliciesForCurrentFeeQuote("{\"invalidpolicy\": 0 }", MockedIdentity);
      RestAuthentication = MockedIdentityBearerAuthentication;
      payloadSubmit = await SubmitTransactionAsync(txHex);
      Assert.AreEqual("failure", payloadSubmit.ReturnResult);
      // status is set only for authenticated user
      await AssertTxStatus(txHash, TxStatus.NodeRejected);
    }

    [DataRow(1)]
    [DataRow(3)]
    [TestMethod]
    public async Task SubmitTransactionsDifferentUsers(int nTxs)
    {
      // unauthorized user has limited maxscriptsizepolicy
      SetPoliciesForCurrentFeeQuote("{\"maxscriptsizepolicy\": 100 }");
      // insert new feeQuote for authorized user (without policies)
      InsertFeeQuote(MockedIdentity);

      // test single update and batch
      List<(string txId, string txHex)> txList = new();
      for (int i = 0; i < nTxs; i++)
      {
        (string txHex, string txId) = CreateNewTransaction();
        txList.Add((txId, txHex));
      }

      var payloadSubmit = await SubmitTransactionsAsync(txList.Select(x => x.txHex).ToArray());
      Assert.IsTrue(payloadSubmit.Txs.All(x => x.ReturnResult == "failure"));

      RestAuthentication = MockedIdentityBearerAuthentication;
      payloadSubmit = await SubmitTransactionsAsync(txList.Select(x => x.txHex).ToArray(), true);
      Assert.IsTrue(payloadSubmit.Txs.All(x => x.ReturnResult == "success"));

      var q = await QueryTransactionStatus(txList.Last().txId);
      Assert.AreEqual("success", q.ReturnResult);

      foreach(var (txId, txHex) in txList)
      {
        // Validate that all of the inputs are already in the database
        await ValidateTxInputsAsync(txHex, txId);
      }
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
      payloadSubmit = await SubmitTransactionAsync(txHex, expectedHttpStatusCode: System.Net.HttpStatusCode.InternalServerError);
      Assert.IsNull(payloadSubmit);

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


          var feequote = FeeQuoteRepository.GetFeeQuoteById(1);
          int deleted = await TxRepositoryPostgres.DeleteTxsWithFeeQuotesAsync(new FeeQuote[] { feequote });
          Assert.AreEqual(1, deleted);
          // resubmit with new (fixed) policy
          payloadSubmit = await SubmitTransactionAsync(txHex);
          Assert.AreEqual("success", payloadSubmit.ReturnResult);
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
      Assert.AreEqual(NodeRejectCode.ResultAlreadyKnown, tx1_payload2.ResultDescription);
    }

    [DataRow(Faults.FaultType.DbBeforeSavingUncommittedState)]
    [DataRow(Faults.FaultType.DbAfterSavingUncommittedState)]
    [TestMethod]
    public async Task ResubmitSameTransactionAfterBlockWasMined(Faults.FaultType dbFaultType)
    {
      InsertFeeQuote(MockedIdentity);
      using CancellationTokenSource cts = new(cancellationTimeout);

      var (txHexAuth, txHashAuth) = CreateNewTransaction();
      var (txHexAnonymous, txHashAnonymous) = CreateNewTransaction();

      mapiMock.SimulateDbFault(dbFaultType, Faults.DbFaultComponent.MapiAfterSendToNode);

      RestAuthentication = MockedIdentityBearerAuthentication;
      var payloadSubmit = await SubmitTransactionAsync(txHexAuth, expectedHttpStatusCode: System.Net.HttpStatusCode.InternalServerError);
      Assert.IsNull(payloadSubmit);

      RestAuthentication = null;
      payloadSubmit = await SubmitTransactionAsync(txHexAnonymous, expectedHttpStatusCode: System.Net.HttpStatusCode.InternalServerError);
      Assert.IsNull(payloadSubmit);
      mapiMock.ClearMode();

      // check txs were accepted to mempool and mine block with them
      var mempoolTxs = await RpcMultiClient.GetRawMempool(cts.Token);
      Assert.IsTrue(mempoolTxs.Contains(txHashAuth));
      Assert.IsTrue(mempoolTxs.Contains(txHashAnonymous));

      await GenerateBlockAndWaitForItToBeInsertedInDBAsync();

      RestAuthentication = MockedIdentityBearerAuthentication;
      payloadSubmit = await SubmitTransactionAsync(txHexAuth);
      Assert.AreEqual("success", payloadSubmit.ReturnResult);

      RestAuthentication = null;
      payloadSubmit = await SubmitTransactionAsync(txHexAnonymous);
      if (dbFaultType == Faults.FaultType.DbBeforeSavingUncommittedState)
      {
        // we don't know, that user already submitted transaction through mAPI the first time,
        // because transaction submitted by anonymous user is sent to node and then saved to DB
        // (for the authenticated user it is already saved before sent to node)
        // but it will still return success because we will discover this tx inside the block and mark it
        // as successfull
        Assert.AreEqual("success", payloadSubmit.ReturnResult);
      }
      else
      {
        Assert.AreEqual("success", payloadSubmit.ReturnResult);
      }
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
            Assert.AreEqual(t.SubmittedAt.Ticks, dbTx.SubmittedAt.Ticks, 1);
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
      List<Tx> txToInsert = new()
      {
        new Tx()
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

    [DataRow(false)]
    [DataRow(true)]
    [TestMethod]
    public async Task SubmitWithUnconfirmedParentsAndGenerateBlockAsync(bool generateBlock)
    {
      mapiMock.SimulateDbFault(Faults.FaultType.DbBeforeSavingUncommittedState, Faults.DbFaultComponent.MapiUnconfirmedAncestors);

      await SubmitWithUnconfirmedParentsAsync(generateBlock);
    }

    [OverrideSetting("AppSettings:ResubmitKnownTransactions", true)]
    [TestMethod]
    public async Task SubmitWithUnconfirmedParentsAndResubmitAsync()
    {
      await SubmitWithUnconfirmedParentsAndGenerateBlockAsync(false);
      await SubmitWithUnconfirmedParentsAndGenerateBlockAsync(true);
    }

    [TestMethod]
    public async Task SubmitWithUnconfirmedParentsTestDifferentFaultsAsync()
    {
      InsertFeeQuote(MockedIdentity);
      RestAuthentication = MockedIdentityBearerAuthentication;

      mapiMock.SimulateDbFault(Faults.FaultType.DbBeforeSavingUncommittedState, Faults.DbFaultComponent.MapiAfterSendToNode);
      await SubmitWithUnconfirmedParentsAsync(false);
    }

    private async Task SubmitWithUnconfirmedParentsAsync(bool generateBlock)
    {
      using CancellationTokenSource cts = new(cancellationTimeout);

      // Create and submit first transaction
      var coin = availableCoins.Dequeue();
      var (txHex1, txId1) = CreateNewTransaction(coin, new Money(1000L));
      var response = await node0.RpcClient.SendRawTransactionAsync(HelperTools.HexStringToByteArray(txHex1), true, false, cts.Token);

      // Create chain based on first transaction with last transaction being submited to mAPI
      var (lastTxHex, lastTxId, mapiCount) = await CreateUnconfirmedAncestorChainAsync(txHex1, txId1, 100, 0, true, cts.Token, System.Net.HttpStatusCode.InternalServerError);

      // Check that first tx is not in database
      long? txInternalId1 = await TxRepositoryPostgres.GetTransactionInternalIdAsync((new uint256(txId1)).ToBytes());
      Assert.IsNull(txInternalId1);

      mapiMock.SimulateDbFault(Faults.FaultType.DbBeforeSavingUncommittedState, Faults.DbFaultComponent.MapiUnconfirmedAncestors);

      var payload = await SubmitTransactionAsync(lastTxHex, true, true);
      Assert.AreEqual("failure", payload.ReturnResult);
      Assert.AreEqual(NodeRejectCode.UnconfirmedAncestorsError, payload.ResultDescription);

      mapiMock.ClearMode();
      if (generateBlock)
      {
        await GenerateBlockAndWaitForItToBeInsertedInDBAsync();
      }

      payload = await SubmitTransactionAsync(lastTxHex, true, true);
      Assert.AreEqual("success", payload.ReturnResult);
      long? lastTxInternalId1 = await TxRepositoryPostgres.GetTransactionInternalIdAsync((new uint256(lastTxId)).ToBytes());
      Assert.IsTrue(lastTxInternalId1.HasValue);
      Assert.AreNotEqual(0, lastTxInternalId1.Value);

      // The chain (with tx1) was inserted, if no block mined
      txInternalId1 = await TxRepositoryPostgres.GetTransactionInternalIdAsync((new uint256(txId1)).ToBytes());
      if (generateBlock)
      {
        Assert.IsNull(txInternalId1);
        return;
      }
      Assert.IsNotNull(txInternalId1);

      // Check unconfirmed ancestors response
      var ua = await rpcClient0.GetMempoolAncestors(lastTxId);
      // sendrawtransactions takes a snapshot of the ancestors in the mempool and
      // prints the inputs of the elements in the snapshot.
      // getmempoolancestors takes a snapshot of the ancestors in the mempool and
      // prints the inputs of the elements in the snapshot ONLY if those inputs are also in the snapshot.
      Assert.AreEqual(100, ua.Transactions.Count);
      Assert.AreEqual(99, ua.Transactions.Count(x => x.Value.Depends.Any())); // sendrawtxs returns 100

      // Validate that all of the inputs are already in the database
      // first txInput is not present in GetMempoolAncestors response
      await ValidateTxInputsAsync(txHex1, txId1, presentOnDB: false);
      await ValidateTxInputsAsync(lastTxHex, lastTxId);
    }
  }
}
