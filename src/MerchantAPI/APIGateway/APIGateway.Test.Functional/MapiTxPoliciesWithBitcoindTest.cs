// Copyright(c) 2021 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain;
using MerchantAPI.Common.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Test.Functional
{
  [TestCategory("TestCategoryNo4")]
  [TestClass]
  public class MapiTxPoliciesWithBitcoindTest : MapiWithBitcoindTestBase
  {

    [TestInitialize]
    public override void TestInitialize()
    {
      base.TestInitialize();
    }


    [TestCleanup]
    public override void TestCleanup()
    {
      base.TestCleanup();
    }

    private static Dictionary<string, object> GetPoliciesDict(string json)
    {
      return HelperTools.JSONDeserialize<Dictionary<string, object>>(json);
    }

    [TestMethod]
    public async Task SubmitTransactionWithInvalidMaxTxSizePolicy()
    {
      var (txHex, _) = CreateNewTransaction();

      (byte[] transaction, bool allowhighfees, bool dontCheckFees, bool listUnconfirmedAncestors, Dictionary<string, object> config)[] transactionsToSubmit =
      {
        (HelperTools.HexStringToByteArray(txHex), true, false, false,  GetPoliciesDict("{\"maxtxsizepolicy\": 100}"))
      };

      // SendRawTransactionAsync does not support config param, only SendRawTransactionsAsync does
      var txResponses = await node0.RpcClient.SendRawTransactionsAsync(transactionsToSubmit, null);
      Assert.AreEqual(1, txResponses.Invalid.Length);
      Assert.AreEqual("Policy value for max tx size must not be less than 99999", txResponses.Invalid.Single().RejectReason);
    }

    [TestMethod]
    public async Task SubmitTransactionWithInvalidPolicyWithUnit()
    {
      var (txHex, _) = CreateNewTransaction();

      // we only support default int/decimal args
      (byte[] transaction, bool allowhighfees, bool dontCheckFees, bool listUnconfirmedAncestors, Dictionary<string, object> config)[] transactionsToSubmit =
      {
        (HelperTools.HexStringToByteArray(txHex), true, false, false, GetPoliciesDict("{\"maxtxsizepolicy\": \"10MB\"}"))
      };

      // SendRawTransactionAsync does not support config param, only SendRawTransactionsAsync does
      var txResponses = await node0.RpcClient.SendRawTransactionsAsync(transactionsToSubmit, null);
      Assert.AreEqual(1, txResponses.Invalid.Length);
      Assert.AreEqual("maxtxsizepolicy must be a number", txResponses.Invalid.Single().RejectReason);
    }

    [TestMethod]
    public async Task SubmitTransactionWithInvalidPolicy()
    {
      // make additional 7 coins
      foreach (var coin in GetCoins(base.rpcClient0, 7))
      {
        availableCoins.Enqueue(coin);
      }

      // we don't use DataRow arguments here, because of performance
      string[] invalidPolicies = new string[] 
      {
      "{\"maxtxsizepolicy\": -1}",
      "{\"datacarriersize\": -1}",
      "{\"maxscriptsizepolicy\": -1}",
      "{\"maxscriptnumlengthpolicy\": -1}",
      "{\"maxstackmemoryusagepolicy\": -1}",
      "{\"limitancestorcount\": -1}",
      "{\"limitcpfpgroupmemberscount\": 1}",
      "{\"acceptnonstdoutputs\": True}",
      "{\"datacarrier\": 0}",
      "{\"maxstdtxvalidationduration\": 0}",
      "{\"maxnonstdtxvalidationduration\": 0}",
      "{\"minconsolidationfactor\": -1}",
      "{\"maxconsolidationinputscriptsize\": -1}",
      "{\"minconfconsolidationinput\": -1}",
      "{\"acceptnonstdconsolidationinput\": 1}"
      };
      foreach(var policy in invalidPolicies)
      {
        SetPoliciesForCurrentFeeQuote(policy);
        var (txHex, _) = CreateNewTransaction();

        var payloadSubmit = await SubmitTransactionAsync(txHex);
        Assert.AreEqual("failure", payloadSubmit.ReturnResult);
      }
    }


    [TestMethod]
    public async Task SubmitAndQueryTransactionStatusWithAllValidPolicies()
    {
      SetPoliciesForCurrentFeeQuote(
        "{\"maxtxsizepolicy\" : 99999, \"datacarriersize\" : 100000, \"maxscriptsizepolicy\" : 100000, " +
        "\"maxscriptnumlengthpolicy\" : 100000, \"maxstackmemoryusagepolicy\" : 10000000, \"limitancestorcount\": 1000," +
        "\"limitcpfpgroupmemberscount\": 10, \"acceptnonstdoutputs\": true, \"datacarrier\": true, " +
        "\"maxstdtxvalidationduration\": 99, \"maxnonstdtxvalidationduration\": 100, \"minconsolidationfactor\": 10, " +
        "\"maxconsolidationinputscriptsize\": 100, \"minconfconsolidationinput\": 10, " +
        "\"acceptnonstdconsolidationinput\": false }");
      var (txHex, txHash) = CreateNewTransaction();

      var payloadSubmit = await SubmitTransactionAsync(txHex);
      Assert.AreEqual("success", payloadSubmit.ReturnResult);

      var q = await QueryTransactionStatus(txHash);
      Assert.AreEqual("success", q.ReturnResult);
      Assert.AreEqual(null, q.Confirmations);

      _ = await rpcClient0.GenerateAsync(1);

      q = await QueryTransactionStatus(txHash);

      Assert.AreEqual("success", q.ReturnResult);
      Assert.AreEqual(1, q.Confirmations);
    }

    [TestMethod]
    public async Task SubmitTransactionWithSingleInvalidPolicy()
    {
      var (txHex, _) = CreateNewTransaction();

      // invalid first maxtxsizepolicy, all others are valid
      SetPoliciesForCurrentFeeQuote(
        "{\"maxtxsizepolicy\" : -1, \"datacarriersize\" : 100000, \"maxscriptsizepolicy\" : 100000, " +
        "\"maxscriptnumlengthpolicy\" : 100000, \"maxstackmemoryusagepolicy\" : 10000000, \"limitancestorcount\": 1000," +
        "\"limitcpfpgroupmemberscount\": 10, \"acceptnonstdoutputs\": true, \"datacarrier\": true, " +
        "\"maxstdtxvalidationduration\": 99, \"maxnonstdtxvalidationduration\": 100, \"minconsolidationfactor\": 10, " +
        "\"maxconsolidationinputscriptsize\": 100, \"minconfconsolidationinput\": 10, " +
        "\"acceptnonstdconsolidationinput\": false }");
      var payloadSubmit = await SubmitTransactionAsync(txHex);
      Assert.AreEqual("failure", payloadSubmit.ReturnResult);

      (txHex, _) = CreateNewTransaction();
      // invalid last acceptnonstdconsolidationinput - second (of duplicated) value is taken
      SetPoliciesForCurrentFeeQuote(
        "{\"maxtxsizepolicy\" : 99999, \"datacarriersize\" : 100000, \"maxscriptsizepolicy\" : 100000, " +
        "\"maxscriptnumlengthpolicy\" : 100000, \"maxstackmemoryusagepolicy\" : 10000000, \"limitancestorcount\": 1000," +
        "\"limitcpfpgroupmemberscount\": 10, \"acceptnonstdoutputs\": true, \"datacarrier\": true, " +
        "\"maxstdtxvalidationduration\": 99, \"maxnonstdtxvalidationduration\": 100, \"minconsolidationfactor\": 10, " +
        "\"maxconsolidationinputscriptsize\": 100, \"minconfconsolidationinput\": 10, " +
        "\"acceptnonstdconsolidationinput\": false, \"acceptnonstdconsolidationinput\": 10 }");
      payloadSubmit = await SubmitTransactionAsync(txHex);
      Assert.AreEqual("failure", payloadSubmit.ReturnResult);

      (txHex, _) = CreateNewTransaction();
      // duplicated acceptnonstdconsolidationinput policy is not a problem
      SetPoliciesForCurrentFeeQuote(
        "{\"maxtxsizepolicy\" : 99999, \"datacarriersize\" : 100000, \"maxscriptsizepolicy\" : 100000, " +
        "\"maxscriptnumlengthpolicy\" : 100000, \"maxstackmemoryusagepolicy\" : 10000000, \"limitancestorcount\": 1000," +
        "\"limitcpfpgroupmemberscount\": 10, \"acceptnonstdoutputs\": true, \"datacarrier\": true, " +
        "\"maxstdtxvalidationduration\": 99, \"maxnonstdtxvalidationduration\": 100, \"minconsolidationfactor\": 10, " +
        "\"maxconsolidationinputscriptsize\": 100, \"minconfconsolidationinput\": 10, " +
        "\"acceptnonstdconsolidationinput\": false, \"acceptnonstdconsolidationinput\": false }");
      payloadSubmit = await SubmitTransactionAsync(txHex);
      Assert.AreEqual("success", payloadSubmit.ReturnResult);
    }

    [TestMethod]
    public async Task SubmitTransactionWithUnknownPolicy()
    {
      var (txHex, _) = CreateNewTransaction();

      // unknown policy is not ignored, so we get failure
      SetPoliciesForCurrentFeeQuote(
        "{\"unknownpolicy\" : 99999 }");
      var payloadSubmit = await SubmitTransactionAsync(txHex);
      Assert.AreEqual("failure", payloadSubmit.ReturnResult);

      (txHex, _) = CreateNewTransaction();

      // test casing - we get failure, because flag is not found
      SetPoliciesForCurrentFeeQuote(
        "{\"maxTxSizepolicy\" : 99999 }");
      payloadSubmit = await SubmitTransactionAsync(txHex);
      Assert.AreEqual("failure", payloadSubmit.ReturnResult);
    }

    [TestMethod]
    public async Task SubmitTransactionWithMaxScriptSizePolicy()
    {
      // input scriptSig = 106bytes
      (string txHex, string txHash) = CreateNewTransaction();

      SetPoliciesForCurrentFeeQuote("{\"maxscriptsizepolicy\": 105 }");
      var payloadSubmit = await SubmitTransactionAsync(txHex);
      Assert.AreEqual("failure", payloadSubmit.ReturnResult);

      var q = await QueryTransactionStatus(txHash);
      Assert.AreEqual("failure", q.ReturnResult);
      Assert.AreEqual(null, q.Confirmations);

      SetPoliciesForCurrentFeeQuote("{\"maxscriptsizepolicy\": 106 }");
      payloadSubmit = await SubmitTransactionAsync(txHex);
      Assert.AreEqual("success", payloadSubmit.ReturnResult);

      q = await QueryTransactionStatus(txHash);
      Assert.AreEqual("success", q.ReturnResult);
      Assert.AreEqual(null, q.Confirmations);

      _ = await rpcClient0.GenerateAsync(1);

      q = await QueryTransactionStatus(txHash);

      Assert.AreEqual("success", q.ReturnResult);
      Assert.AreEqual(1, q.Confirmations);
    }

    [TestMethod]
    public async Task SubmitTransactionDifferentUsers()
    {
      // unauthorized user has limited maxscriptsizepolicy
      SetPoliciesForCurrentFeeQuote("{\"maxscriptsizepolicy\": 105 }");
      // insert same feeQuote as for unauthorized user
      InsertFeeQuote(MockedIdentity);

      (string txHex, string _) = CreateNewTransaction();

      var payloadSubmit = await SubmitTransactionAsync(txHex);
      Assert.AreEqual("failure", payloadSubmit.ReturnResult);

      RestAuthentication = MockedIdentityBearerAuthentication;
      payloadSubmit = await SubmitTransactionAsync(txHex);
      Assert.AreEqual("success", payloadSubmit.ReturnResult);
    }

    [TestMethod]
    public async Task SubmitAndQueryTransactionStatusWithNonStdOutputsPolicy()
    {
      SetPoliciesForCurrentFeeQuote("{ \"acceptnonstdoutputs\": false}");
      // for success value must be bigger than 27500
      var tx1 = CreateNewTransactionTx(28000L);

      var payloadSubmit1 = await SubmitTransactionAsync(tx1.ToHex());
      Assert.AreEqual("success", payloadSubmit1.ReturnResult);

      var (txHex2, txHash2) = CreateNewTransactionWithData(tx1);
      var payloadSubmit2 = await SubmitTransactionAsync(txHex2);
      Assert.AreEqual("failure", payloadSubmit2.ReturnResult);

      SetPoliciesForCurrentFeeQuote("{ \"acceptnonstdoutputs\": true}");
      payloadSubmit2 = await SubmitTransactionAsync(txHex2);
      Assert.AreEqual("success", payloadSubmit2.ReturnResult);

      var q = await QueryTransactionStatus(txHash2);
      Assert.AreEqual("success", q.ReturnResult);
      Assert.AreEqual(null, q.Confirmations);

      _ = await rpcClient0.GenerateAsync(1);

      q = await QueryTransactionStatus(tx1.GetHash().ToString());

      Assert.AreEqual("success", q.ReturnResult);
      Assert.AreEqual(1, q.Confirmations);

      q = await QueryTransactionStatus(txHash2);
      Assert.AreEqual("success", q.ReturnResult);
      Assert.AreEqual(1, q.Confirmations);
    }

    [TestMethod]
    public async Task SubmitTransactionsWithLimitAncestorCount()
    {
      SetPoliciesForCurrentFeeQuote("{\"limitancestorcount\": 1 }");

      // no ancestor
      var tx1 = CreateNewTransactionTx();
      var parentTxCoin = new Coin(tx1, 0);
      // one ancestor
      var (tx2Hex, tx2Id) = CreateNewTransaction(parentTxCoin, new Money(1000L));

      var payloadSubmit = await SubmitTransactionsAsync(new string[] { tx1.ToHex(), tx2Hex });
      Assert.AreEqual(1, payloadSubmit.FailureCount);
      var txFailure = payloadSubmit.Txs.Single(x => x.ReturnResult == "failure");
      Assert.AreEqual(tx2Id, txFailure.Txid);
      Assert.AreEqual(NodeRejectCode.MapiRetryMempoolErrorWithDetails(NodeRejectCode.MapiRetryCodesAndReasons[0]), txFailure.ResultDescription);
    }

    [DataRow("{\"skipscriptflags\": [\"MINIMALDATA\"] }")]
    [DataRow("{\"skipscriptflags\": [\"MINIMALDATA\", \"DERSIG\", \"NULLDUMMY\", \"DISCOURAGE_UPGRADABLE_NOPS\", \"CLEANSTACK\"]}")]
    [TestMethod]
    public async Task SubmitTransactionWithSkipScriptFlags(string skipScriptFlags)
    {
      // skipScriptFlags supports comma separated list of strings.
      (string txHex, string _) = CreateNewTransaction();

      SetPoliciesForCurrentFeeQuote(skipScriptFlags);
      var payloadSubmit = await SubmitTransactionAsync(txHex);
      Assert.AreEqual("success", payloadSubmit.ReturnResult);
    }

    [DataRow("{\"skipscriptflags\": 105 }")]
    [DataRow("{\"skipscriptflags\": -1 }")]
    [DataRow("{\"skipscriptflags\": \"MINIMALDATA\"}")]
    [DataRow("{\"skipscriptflags\": \"CLEANSTACK,DERSIG\"}")]
    [TestMethod]
    public async Task SubmitTransactionWithSkipScriptFlagsInvalid(string skipScriptFlags)
    {
      (string txHex, string _) = CreateNewTransaction();

      SetPoliciesForCurrentFeeQuote(skipScriptFlags);
      var payloadSubmit = await SubmitTransactionAsync(txHex);
      Assert.AreEqual("failure", payloadSubmit.ReturnResult);
    }

    [TestMethod]
    public async Task SubmitTransactionMixUnlimitedPoliciesSkipScriptFlags()
    {
      (string txHex, string _) = CreateNewTransaction();

      SetPoliciesForCurrentFeeQuote(
        "{\"maxtxsizepolicy\": 0, \"maxscriptsizepolicy\": 0, \"maxscriptnumlengthpolicy\": 0, " +
        "\"skipscriptflags\": [\"MINIMALDATA\", \"DERSIG\", \"NULLDUMMY\", \"DISCOURAGE_UPGRADABLE_NOPS\", \"CLEANSTACK\"]}");
      var payloadSubmit = await SubmitTransactionAsync(txHex);
      Assert.AreEqual("success", payloadSubmit.ReturnResult);
    }
  }
}
