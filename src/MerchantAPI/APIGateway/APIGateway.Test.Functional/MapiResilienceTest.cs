// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain;
using MerchantAPI.APIGateway.Domain.Actions;
using MerchantAPI.APIGateway.Domain.Models.Faults;
using MerchantAPI.APIGateway.Domain.ViewModels;
using MerchantAPI.APIGateway.Rest.ViewModels;
using MerchantAPI.APIGateway.Rest.ViewModels.Faults;
using MerchantAPI.APIGateway.Test.Functional.Attributes;
using MerchantAPI.APIGateway.Test.Functional.Mock;
using MerchantAPI.APIGateway.Test.Functional.Server;
using MerchantAPI.Common.BitcoinRpc.Responses;
using MerchantAPI.Common.Json;
using MerchantAPI.Common.Test;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text.Json;
using System.Threading.Tasks;
using static MerchantAPI.Common.BitcoinRpc.Responses.RpcSendTransactions;

namespace MerchantAPI.APIGateway.Test.Functional
{
  [TestCategory("TestCategoryNo1")]
  [TestClass]
  public class MapiResilienceTest : MapiTestBase
  {
    MapiMock mapiMock;

    [TestInitialize]
    public override void TestInitialize()
    {
      base.TestInitialize();
      mapiMock = server.Services.GetRequiredService<IMapi>() as MapiMock;

      LoadFeeQuotesFromJsonAndInsertToDbAsync().Wait();
    }

    public override TestServer CreateServer(bool mockedServices, TestServer serverCallback, string dbConnectionString, IEnumerable<KeyValuePair<string, string>> overridenSettings = null)
    {
      return new TestServerBase(DbConnectionStringDDL).CreateServer<MapiServer, APIGatewayTestsMockWithDBInsertStartup, APIGatewayTestsStartup>(mockedServices, serverCallback, dbConnectionString, overridenSettings);
    }

    [TestCleanup]
    public override void TestCleanup()
    {
       base.TestCleanup();
    }

    public async Task SubmitTxModeNormalAsync(string txHex, string txHash, string expectedResult="success", string expectedDescription="")
    {
      mapiMock.ClearMode();
      await AssertSubmitTxAsync(txHex, txHash, expectedResult, expectedDescription);
    }

    [TestMethod]
    public async Task SubmitTxJsonNodeFailsWhenSendRawTxs()
    {
      mapiMock.SimulateMode(Faults.SimulateSendTxsResponse.NodeFailsWhenSendRawTxs);

      var response = await SubmitTxToMapiAsync(txC3Hex, HttpStatusCode.InternalServerError);
      Assert.IsNull(response.response);
      await CheckHttpResponseMessageDetailAsync(response.httpResponse,
        "Error while submitting transactions to the node - no response or error returned.");

      await SubmitTxModeNormalAsync(txC3Hex, txC3Hash);
    }


    [TestMethod]
    public async Task SubmitTransactionJsonAuthenticated()
    {
      // use special free fee policy for user
      await LoadFeeQuotesFromJsonAndInsertToDbAsync("feeQuotesWithIdentity.json");

      var reqContent = new StringContent($"{{ \"rawtx\": \"{txZeroFeeHex}\" }}");
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);

      // txZeroFeeHex - it should fail without authentication
      var response = await Post<SignedPayloadViewModel>(MapiServer.ApiMapiSubmitTransaction, Client, reqContent, HttpStatusCode.OK);
      VerifySignature(response);
      Assert.AreEqual(0, rpcClientFactoryMock.AllCalls.FilterCalls("mocknode0:sendrawtransactions/").Count()); // no calls, to submit txs since we do not pay enough fee

      var payload = response.response.ExtractPayload<SubmitTransactionResponseViewModel>();
      // Check if all fields are set
      await AssertIsOKAsync(payload, txZeroFeeHash, "failure", "Not enough fees");

      // Test token valid until year 2030. Generate with:
      //    TokenManager.exe generate -n 5 -i http://mysite.com -a http://myaudience.com -k thisisadevelopmentkey -d 3650
      //
      RestAuthentication = MockedIdentityBearerAuthentication;
      // now it should succeed for this user
      response = await Post<SignedPayloadViewModel>(MapiServer.ApiMapiSubmitTransaction, Client, reqContent, HttpStatusCode.OK);
      VerifySignature(response);

      rpcClientFactoryMock.AllCalls.AssertContains("mocknode0:sendrawtransactions/", "mocknode0:sendrawtransactions/" + txZeroFeeHash);
      payload = response.response.ExtractPayload<SubmitTransactionResponseViewModel>();

      // Check if all fields are set
      await AssertIsOKAsync(payload, txZeroFeeHash);
    }

    [DataRow(false)]
    [DataRow(true)]
    [TestMethod]
    public async Task SubmitTxJsonNodeFailsAfterSendRawTxs(bool authenticated)
    {
      await LoadFeeQuotesFromJsonAndInsertToDbAsync("feeQuotesWithIdentity.json");

      if (authenticated)
      {
        RestAuthentication = MockedIdentityBearerAuthentication;
      }

      mapiMock.SimulateMode(Faults.SimulateSendTxsResponse.NodeFailsAfterSendRawTxs);

      var response = await SubmitTxToMapiAsync(txC3Hex, HttpStatusCode.OK);
      VerifySignature(response);
      var payload = response.response.ExtractPayload<SubmitTransactionResponseViewModel>();

      await AssertIsOKAsync(payload, txC3Hash, "success");

      await SubmitTxModeNormalAsync(txC3Hex, txC3Hash, "success", NodeRejectCode.ResultAlreadyKnown);
    }

    [DataRow(false)]
    [DataRow(true)]
    [TestMethod]
    public async Task SubmitTxJsonMapiFailsAfterSendRawTxsAndDbSave(bool authenticated)
    {
      await LoadFeeQuotesFromJsonAndInsertToDbAsync("feeQuotesWithIdentity.json");
      if (authenticated)
      {
        RestAuthentication = MockedIdentityBearerAuthentication;
      }
      mapiMock.SimulateDbFault(Faults.FaultType.DbAfterSavingUncommittedState, Faults.DbFaultComponent.MapiAfterSendToNode);

      var response = await SubmitTxToMapiAsync(txC3Hex, HttpStatusCode.InternalServerError);
      var faultmanager = server.Services.GetRequiredService<IFaultManager>();
      var fault = faultmanager.GetList().Single();
      await CheckHttpResponseMessageDetailAsync(response.httpResponse, $"Fault (no name) id {fault.Id}");

      Assert.IsNull(response.response);

      await AssertTxStatus(txC3Hash, TxStatus.Accepted);

      await SubmitTxModeNormalAsync(txC3Hex, txC3Hash, expectedDescription: NodeRejectCode.ResultAlreadyKnown);
    }

    [DataRow(false)]
    [DataRow(true)]
    [TestMethod]
    public async Task SubmitTxJsonMapiFailsAfterSendRawTxs(bool authenticated)
    {
      await LoadFeeQuotesFromJsonAndInsertToDbAsync("feeQuotesWithIdentity.json");
      if (authenticated)
      {
        RestAuthentication = MockedIdentityBearerAuthentication;
      }
      mapiMock.SimulateDbFault(Faults.FaultType.DbBeforeSavingUncommittedState, Faults.DbFaultComponent.MapiAfterSendToNode);

      var response = await SubmitTxToMapiAsync(txC3Hex, HttpStatusCode.InternalServerError);

      Assert.IsNull(response.response);

      if (authenticated)
      {
        await AssertTxStatus(txC3Hash, TxStatus.SentToNode);
      }
      else
      {
        await AssertTxStatus(txC3Hash, TxStatus.NotPresentInDb);
      }

      await SubmitTxModeNormalAsync(txC3Hex, txC3Hash);
    }

    [DataRow(false)]
    [DataRow(true)]
    [TestMethod]
    public async Task SubmitTxsBatchJsonMapiFailsAfterSendRawTxs(bool authenticated)
    {
      if (authenticated)
      {
        await LoadFeeQuotesFromJsonAndInsertToDbAsync("feeQuotesWithIdentity.json");
        RestAuthentication = MockedIdentityBearerAuthentication;
      }

      mapiMock.SimulateDbFault(Faults.FaultType.DbBeforeSavingUncommittedState, Faults.DbFaultComponent.MapiAfterSendToNode);

      var response = await SubmitTxsToMapiAsync(HttpStatusCode.InternalServerError);

      Assert.IsNull(response.response);

      if (authenticated)
      {
        await AssertTxStatus(txC3Hash, TxStatus.SentToNode);
        await AssertTxStatus(txZeroFeeHash, TxStatus.SentToNode);
        await AssertTxStatus(tx2Hash, TxStatus.SentToNode);
      }
      else
      {
        await AssertTxStatus(txC3Hash, TxStatus.NotPresentInDb);
        await AssertTxStatus(txZeroFeeHash, TxStatus.NotPresentInDb);
        await AssertTxStatus(tx2Hash, TxStatus.NotPresentInDb);
      }

      mapiMock.ClearMode();

      response = await SubmitTxsToMapiAsync(HttpStatusCode.OK);
      VerifySignature(response);

      var payload = response.response.ExtractPayload<SubmitTransactionsResponseViewModel>();
      await ValidateHeaderSubmitTransactionsAsync(payload);

      await AssertTxStatus(txC3Hash, TxStatus.Accepted);
      await AssertTxStatus(tx2Hash, TxStatus.Accepted);
      if (authenticated)
      {
        await AssertTxStatus(txZeroFeeHash, TxStatus.Accepted);
      }
      else
      {
        await AssertTxStatus(txZeroFeeHash, TxStatus.NotPresentInDb); // failure
      }
    }

    [DataRow(TxStatus.NodeRejected)]
    [DataRow(TxStatus.SentToNode)]
    [DataRow(TxStatus.UnknownOldTx)]
    [DataRow(TxStatus.Accepted)]
    [DataRow(TxStatus.MissingInputsMaxRetriesReached)]
    [OverrideSetting("AppSettings:ResubmitKnownTransactions", true)]
    [TestMethod]
    public async Task SubmitSameTransactionTestInvalidParametersAsync(int txStatus)
    {
      var (txHex1, txId1) = (txC3Hex, txC3Hash);

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
          TxStatus = txStatus,
          PolicyQuoteId = 1
        }
      };
      await TxRepositoryPostgres.InsertOrUpdateTxsAsync(txToInsert, false);

      if (txStatus == TxStatus.NodeRejected)
      {
        var response = await SubmitTxToMapiAsync(txHex1, HttpStatusCode.OK, true, true, "TSC");
        VerifySignature(response);
        var payload = response.response.ExtractPayload<SubmitTransactionResponseViewModel>();
        Assert.AreEqual("success", payload.ReturnResult);

        var tx = await TxRepositoryPostgres.GetTransactionAsync(new uint256(txId1).ToBytes());
        Assert.AreEqual(TxStatus.Accepted, tx.TxStatus);
        Assert.AreEqual(true, tx.DSCheck);
        Assert.AreEqual(true, tx.MerkleProof);
        Assert.AreEqual("TSC", tx.MerkleFormat);
        Assert.IsNotNull(tx.CallbackUrl);
        Assert.IsTrue(tx.SetPolicyQuote);
      }
      else
      {
        // parameter changed - submit should fail
        var response = await SubmitTxToMapiAsync(txHex1, HttpStatusCode.OK);
        AssertSubmitTxFailedBecauseOfDifferentParams(response);
        response = await SubmitTxToMapiAsync(txHex1, HttpStatusCode.OK, dsCheck: true, customCallbackUrl: null);
        AssertSubmitTxFailedBecauseOfDifferentParams(response);
        response = await SubmitTxToMapiAsync(txHex1, HttpStatusCode.OK, merkleProof: true, customCallbackUrl: null);
        AssertSubmitTxFailedBecauseOfDifferentParams(response);

        var tx = await TxRepositoryPostgres.GetTransactionAsync(new uint256(txId1).ToBytes());
        Assert.AreEqual(txStatus, tx.TxStatus);
        Assert.AreEqual(false, tx.DSCheck);
        Assert.AreEqual(false, tx.MerkleProof);
        Assert.AreEqual(null, tx.MerkleFormat);
        Assert.IsNull(tx.CallbackUrl);
        Assert.IsFalse(tx.SetPolicyQuote);
      }
    }

    private static void AssertSubmitTxFailedBecauseOfDifferentParams((SignedPayloadViewModel response, HttpResponseMessage httpResponse) response)
    {
      VerifySignature(response);
      var payload = response.response.ExtractPayload<SubmitTransactionResponseViewModel>();
      Assert.AreEqual("failure", payload.ReturnResult);
      Assert.AreEqual("Transaction already submitted with different parameters.", payload.ResultDescription);
    }

    [DataRow(TxStatus.NodeRejected)]
    [DataRow(TxStatus.SentToNode)]
    [DataRow(TxStatus.UnknownOldTx)]
    [DataRow(TxStatus.Accepted)]
    [DataRow(TxStatus.MissingInputsMaxRetriesReached)]
    [TestMethod]
    public async Task SubmitSameTransactionTestWarningsAsync(int txStatus)
    {
      await LoadFeeQuotesFromJsonAndInsertToDbAsync("feeQuotesWithIdentity.json");
      RestAuthentication = MockedIdentityBearerAuthentication;

      var (txHex1, txId1) = (txC3Hex, txC3Hash);

      // Store tx to database before submitting it to the mAPI
      List<Domain.Models.Tx> txToInsert = new()
      {
        new Domain.Models.Tx()
        {
          TxPayload = HelperTools.HexStringToByteArray(txHex1),
          TxExternalId = new uint256(txId1),
          ReceivedAt = DateTime.UtcNow,
          MerkleProof = false,
          DSCheck = true,
          CallbackUrl = CallbackFunctionalTests.Url,
          TxStatus = txStatus,
          PolicyQuoteId = 2
        }
      };
      await TxRepositoryPostgres.InsertOrUpdateTxsAsync(txToInsert, false);

      var response = await SubmitTxToMapiAsync(txHex1, HttpStatusCode.OK, true);
      VerifySignature(response);
      var payload = response.response.ExtractPayload<SubmitTransactionResponseViewModel>();
      Assert.AreEqual("success", payload.ReturnResult);
      Assert.IsTrue(payload.Warnings.Any());
      Assert.AreEqual(Warning.MissingDSNT, payload.Warnings.Single());
    }

    [DataRow(TxStatus.NodeRejected)]
    [DataRow(TxStatus.SentToNode)]
    [DataRow(TxStatus.UnknownOldTx)]
    [DataRow(TxStatus.Accepted)]
    [DataRow(TxStatus.MissingInputsMaxRetriesReached)]
    [OverrideSetting("AppSettings:ResubmitKnownTransactions", true)]
    [TestMethod]
    public async Task SubmitSameTransactionTestWarningsAndResubmitAsync(int txStatus)
    {
      await SubmitSameTransactionTestWarningsAsync(txStatus);
    }

    [TestMethod]
    public async Task SubmitSameTransactionAfterRejectedAsync()
    {
      await LoadFeeQuotesFromJsonAndInsertToDbAsync("feeQuotesWithIdentity.json");

      var (txHex1, txId1) = (txC3Hex, txC3Hash);

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
          TxStatus = TxStatus.NodeRejected,
          PolicyQuoteId = 1
        }
      };
      await TxRepositoryPostgres.InsertOrUpdateTxsAsync(txToInsert, false);

      RestAuthentication = MockedIdentityBearerAuthentication;

      var response = await SubmitTxToMapiAsync(txHex1, HttpStatusCode.OK, true, true);
      VerifySignature(response);
      var payload = response.response.ExtractPayload<SubmitTransactionResponseViewModel>();
      Assert.AreEqual("success", payload.ReturnResult);
      Assert.IsTrue(payload.Warnings.Any());
      Assert.AreEqual(Warning.MissingDSNT, payload.Warnings.Single());

      var tx = await TxRepositoryPostgres.GetTransactionAsync(new uint256(txId1).ToBytes());
      Assert.AreEqual(TxStatus.Accepted, tx.TxStatus);
      Assert.AreEqual(2, tx.PolicyQuoteId);
      Assert.AreEqual(true, tx.DSCheck);
      Assert.AreEqual(true, tx.MerkleProof);
    }

    [DataRow(Faults.SimulateSendTxsResponse.NodeReturnsNonStandard)]
    [DataRow(Faults.SimulateSendTxsResponse.NodeReturnsInsufficientFee)]
    [DataRow(Faults.SimulateSendTxsResponse.NodeReturnsMempoolFull)]
    [DataRow(Faults.SimulateSendTxsResponse.NodeReturnsMempoolFullNonFinal)]
    [DataRow(Faults.SimulateSendTxsResponse.NodeReturnsEvicted)]
    [TestMethod]
    public async Task SubmitTxJsonNodeReturnsMempoolError(Faults.SimulateSendTxsResponse mockMode)
    {
      mapiMock.SimulateMode(mockMode);

      var response = await SubmitTxToMapiAsync(txC3Hex, HttpStatusCode.OK);
      VerifySignature(response);

      var payload = response.response.ExtractPayload<SubmitTransactionResponseViewModel>();

      await AssertIsOKAsync(payload, txC3Hash, "failure", 
        NodeRejectCode.MapiRetryMempoolErrorWithDetails(NodeRejectCode.MapiRetryCodesAndReasons[(int)mockMode-1]));

      await SubmitTxModeNormalAsync(txC3Hex, txC3Hash);
    }

    [TestMethod]
    public async Task SubmitTxJsonNodeReturnsMempoolErrorWithMoreInfo()
    {
      // check that MempoolFull is classified as retryable
      // even when node returns reason with additional information
      var (code, reason) = NodeRejectCode.MempoolFullCodeAndReason;
      var rpcResult = new RpcSendTransactions();
      List<RpcInvalidTx> txsInvalid = new();
      RpcInvalidTx txInvalid = new()
      {
        RejectCode = code,
        RejectReason = $"{ reason } Removed: {txC3Hash}",
        Txid = txC3Hash
      };
      txsInvalid.Add(txInvalid);
      rpcResult.Invalid = txsInvalid.ToArray();

      rpcClientFactoryMock.SetUpPredefinedResponse(("mocknode0:sendrawtransactions", rpcResult));

      var response = await SubmitTxToMapiAsync(txC3Hex, HttpStatusCode.OK);
      VerifySignature(response);

      var payload = response.response.ExtractPayload<SubmitTransactionResponseViewModel>();
      //  mAPI must return error as MapiRetryMempoolError 
      await AssertIsOKAsync(payload, txC3Hash, "failure",
        NodeRejectCode.MapiRetryMempoolErrorWithDetails(NodeRejectCode.CombineRejectCodeAndReason(txInvalid.RejectCode, txInvalid.RejectReason)));
    }

    [DataRow(Faults.SimulateSendTxsResponse.NodeReturnsNonStandard)]
    [DataRow(Faults.SimulateSendTxsResponse.NodeReturnsInsufficientFee)]
    [DataRow(Faults.SimulateSendTxsResponse.NodeReturnsMempoolFull)]
    [DataRow(Faults.SimulateSendTxsResponse.NodeReturnsMempoolFullNonFinal)]
    [DataRow(Faults.SimulateSendTxsResponse.NodeReturnsEvicted)]
    [TestMethod]
    public async Task SubmitTxsBatchJsonNodeReturnsMempoolError(Faults.SimulateSendTxsResponse mockMode)
    {
      mapiMock.SimulateMode(mockMode);

      var response = await SubmitTxsToMapiAsync(HttpStatusCode.OK);
      VerifySignature(response);

      var payload = response.response.ExtractPayload<SubmitTransactionsResponseViewModel>();
      await ValidateHeaderSubmitTransactionsAsync(payload);
      Assert.IsTrue(payload.Txs.All(x => x.ReturnResult == "failure"));
      Assert.AreEqual("Not enough fees", payload.Txs[0].ResultDescription);
      var error = NodeRejectCode.MapiRetryMempoolErrorWithDetails(NodeRejectCode.MapiRetryCodesAndReasons[(int)mockMode-1]);
      Assert.AreEqual(error, payload.Txs[1].ResultDescription);
      Assert.AreEqual(error, payload.Txs[2].ResultDescription);
    }

    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    [TestMethod]
    public async Task SubmitSameTransactionInParallelAsync(bool dbAuthenticated, bool bearerAuthentication)
    {
      // we want to simulate situation, where the same tx is sent in parallel in two different submits
      // we save tx to database with 'testStatus' status and simulate mapi calls to db
      int testStatus = 100;
      await LoadFeeQuotesFromJsonAndInsertToDbAsync("feeQuotesWithIdentity.json");

      // saved policyQuoteId is synonymous to authentication: 1 = anonymous, 2 = authenticated
      int policyQuoteId = 1;
      if (dbAuthenticated)
      {
        policyQuoteId = 2;
      }

      var (txHex1, txId1) = (txC3Hex, txC3Hash);
      (byte[] transaction, bool allowhighfees, bool dontCheckFees, bool listUnconfirmedAncestors, Dictionary<string, object> config)[] transactionsToSubmit =
{
        (HelperTools.HexStringToByteArray(txHex1), true, false, false,  null)
      };

      await RpcMultiClient.SendRawTransactionsAsync(transactionsToSubmit);

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
          CallbackUrl = CallbackFunctionalTests.Url,
          TxStatus = testStatus,
          PolicyQuoteId = policyQuoteId
        }
      };
      var inserted = await TxRepositoryPostgres.InsertOrUpdateTxsAsync(txToInsert, false, true, true);
      Assert.AreEqual(txToInsert[0].TxExternalId, new uint256(inserted[0]));

      // the tx in database should not be updated
      if (bearerAuthentication)
      {
        List<Domain.Models.Tx> txToUpdate = new()
        {
          new Domain.Models.Tx()
          {
            TxPayload = HelperTools.HexStringToByteArray(txHex1),
            TxExternalId = new uint256(txId1),
            ReceivedAt = DateTime.UtcNow,
            MerkleProof = false,
            DSCheck = false,
            CallbackUrl = CallbackFunctionalTests.Url,
            TxStatus = TxStatus.SentToNode,
            PolicyQuoteId = policyQuoteId
          }
        };
        inserted = await TxRepositoryPostgres.InsertOrUpdateTxsAsync(txToUpdate, false, false, true);
        Assert.IsTrue(inserted.Length == 0);
        RestAuthentication = MockedIdentityBearerAuthentication;
      }

      List<Domain.Models.Tx> txToUpdateAfterSentNoded = new()
      {
        new Domain.Models.Tx()
        {
          TxPayload = HelperTools.HexStringToByteArray(txHex1),
          TxExternalId = new uint256(txId1),
          ReceivedAt = DateTime.UtcNow,
          MerkleProof = false,
          DSCheck = false,
          CallbackUrl = CallbackFunctionalTests.Url,
          TxStatus = TxStatus.Accepted,
          PolicyQuoteId = policyQuoteId,
          UpdateTx = (bearerAuthentication && inserted.Length > 0) ? Domain.Models.Tx.UpdateTxMode.TxStatusAndResubmittedAt : Domain.Models.Tx.UpdateTxMode.Insert
        }
      };

      inserted =  await TxRepositoryPostgres.InsertOrUpdateTxsAsync(txToUpdateAfterSentNoded, false, true, true);
      Assert.IsTrue(inserted.Length == 0);

      var response = await SubmitTxToMapiAsync(txHex1, HttpStatusCode.OK);
      VerifySignature(response);
      var payload = response.response.ExtractPayload<SubmitTransactionResponseViewModel>();
      if (dbAuthenticated == bearerAuthentication)
      {
        Assert.AreEqual("success", payload.ReturnResult);

        var tx = await TxRepositoryPostgres.GetTransactionAsync(new uint256(txId1).ToBytes());
        Assert.AreEqual(testStatus, tx.TxStatus);
        Assert.AreEqual(policyQuoteId, tx.PolicyQuoteId);
      }
      else
      {
        Assert.AreEqual("failure", payload.ReturnResult);
        Assert.AreEqual("Transaction already submitted with different parameters.", payload.ResultDescription);
      }
    }

    [DataRow(false)]
    [DataRow(true)]
    [TestMethod]
    public async Task SubmitTransactionSentToNodeDifferentUsersAsync(bool authenticated)
    {
      await LoadFeeQuotesFromJsonAndInsertToDbAsync("feeQuotesWithIdentity.json");
      // saved policyQuoteId is synonymous to authentication:
      // 2 and 3 policyQuotes are from two different authenticated users
      var (txHex1, txId1) = (txC3Hex, txC3Hash);
      (byte[] transaction, bool allowhighfees, bool dontCheckFees, bool listUnconfirmedAncestors, Dictionary<string, object> config)[] transactionsToSubmit =
{
        (HelperTools.HexStringToByteArray(txHex1), true, false, false,  null)
      };

      await RpcMultiClient.SendRawTransactionsAsync(transactionsToSubmit);

      // Store tx to database before submitting it to the mAPI
      int policyQuoteId = 3;
      List<Domain.Models.Tx> txToInsert = new()
      {
        new Domain.Models.Tx()
        {
          TxPayload = HelperTools.HexStringToByteArray(txHex1),
          TxExternalId = new uint256(txId1),
          ReceivedAt = DateTime.UtcNow,
          MerkleProof = false,
          DSCheck = false,
          TxStatus = TxStatus.SentToNode,
          PolicyQuoteId = policyQuoteId
        }
      };
      await TxRepositoryPostgres.InsertOrUpdateTxsAsync(txToInsert, false);

      if (authenticated)
      {
        // policyQuoteId = 2
        RestAuthentication = MockedIdentityBearerAuthentication;
      }

      var response = await SubmitTxToMapiAsync(txHex1, HttpStatusCode.OK);
      VerifySignature(response);
      var payload = response.response.ExtractPayload<SubmitTransactionResponseViewModel>();
      Assert.AreEqual("failure", payload.ReturnResult);
      Assert.AreEqual("Transaction already submitted with different parameters.", payload.ResultDescription);

      await AssertTxStatus(txId1, TxStatus.SentToNode);
    }

    [TestMethod]
    [OverrideSetting("AppSettings:CheckFeeDisabled", true)]
    public async Task SubmitTransactionJsonCheckFeeDisabled()
    {
      var reqContent = GetJsonRequestContent(txZeroFeeHex);
      await LoadFeeQuotesFromJsonAndInsertToDbAsync("feeQuotesWithIdentity.json");
      RestAuthentication = MockedIdentityBearerAuthentication;

      var response = await Post<SignedPayloadViewModel>(MapiServer.ApiMapiSubmitTransaction, Client, reqContent, HttpStatusCode.OK);
      VerifySignature(response);

      Assert.AreEqual(1, rpcClientFactoryMock.AllCalls.FilterCalls("mocknode0:sendrawtransactions/").Count());

      var payload = response.response.ExtractPayload<SubmitTransactionResponseViewModel>();

      // Check if all fields are set
      await AssertIsOKAsync(payload, txZeroFeeHash);
    }

    [TestMethod]
    [OverrideSetting("AppSettings:ResubmitKnownTransactions", true)]
    public async Task ResubmitTxWithCheckFeeDisabledAndUnconfirmedAncestorsCallAsync()
    {
      // tx has ListUnconfirmedAncestors = true on submit
      var (txHex, txId) = (txC3Hex, txC3Hash);

      // Store tx to database before submitting it to the mAPI
      List<Domain.Models.Tx> txToInsert = new()
      {
        new Domain.Models.Tx()
        {
          TxPayload = HelperTools.HexStringToByteArray(txHex),
          TxExternalId = new uint256(txId),
          ReceivedAt = DateTime.UtcNow,
          MerkleProof = false,
          DSCheck = true,
          CallbackUrl = CallbackFunctionalTests.Url,
          TxStatus = TxStatus.Accepted,
          PolicyQuoteId = 1,
          SetPolicyQuote = false // is set to false when CheckFeeDisabled
        }
      };
      await TxRepositoryPostgres.InsertOrUpdateTxsAsync(txToInsert, false);

      var response = await SubmitTxToMapiAsync(txHex, HttpStatusCode.OK, true);
      VerifySignature(response);
      var payload = response.response.ExtractPayload<SubmitTransactionResponseViewModel>();
      Assert.AreEqual("success", payload.ReturnResult);
    }

    [TestMethod]
    [OverrideSetting("AppSettings:EnableFaultInjection", false)]
    public async Task PostWithDisabledFaultInjection()
    {
      var entryPost = new FaultTriggerViewModelCreate()
      {
        Id = "1a",
        Type = Faults.FaultType.DbBeforeSavingUncommittedState.ToString(),
        DbFaultComponent = Faults.DbFaultComponent.MapiAfterSendToNode.ToString(),
        FaultProbability = 100,
        DbFaultMethod = Faults.DbFaultMethod.Exception.ToString()
      };
      var reqContent = new StringContent(
        JsonSerializer.Serialize(entryPost)
      );
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);
      ApiKeyAuthentication = AppSettings.RestAdminAPIKey;
      RestAuthentication = null;

      // fault endpoints must be unresponsive
      await Post<FaultTriggerViewModelGet>(MapiServer.TestFaultUrl, Client, reqContent, HttpStatusCode.InternalServerError);

      await Get<FaultTriggerViewModelGet[]>(Client, MapiServer.TestFaultUrl, HttpStatusCode.InternalServerError);

      // submit works normally
      await SubmitTxToMapiAsync(txC3Hex, HttpStatusCode.OK);
    }
  }
}
