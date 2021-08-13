// Copyright(c) 2021 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.Common.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Test.Functional
{

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

    private Dictionary<string, object> GetPoliciesDict(string json)
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
      Assert.AreEqual(1, txResponses.Invalid.Count());
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
      Assert.AreEqual(1, txResponses.Invalid.Count());
      Assert.AreEqual("maxtxsizepolicy must be a number", txResponses.Invalid.Single().RejectReason);
    }

    [DataRow("{\"maxtxsizepolicy\": -1}")]
    [DataRow("{\"datacarriersize\": -1}")]
    [DataRow("{\"maxscriptsizepolicy\": -1}")]
    [DataRow("{\"maxscriptnumlengthpolicy\": -1}")]
    [DataRow("{\"maxstackmemoryusagepolicy\": -1}")]
    [DataRow("{\"limitancestorcount\": -1}")]
    [DataRow("{\"limitcpfpgroupmemberscount\": 1}")]
    [DataRow("{\"acceptnonstdoutputs\": True}")]
    [DataRow("{\"datacarrier\": 0}")]
    [DataRow("{\"dustrelayfee\": 0.1}")]
    [DataRow("{\"maxstdtxvalidationduration\": 0}")]
    [DataRow("{\"maxnonstdtxvalidationduration\": 0}")]
    [DataRow("{\"minconsolidationfactor\": -1}")]
    [DataRow("{\"maxconsolidationinputscriptsize\": -1}")]
    [DataRow("{\"minconfconsolidationinput\": -1}")]
    //[DataRow("{\"minconsolidationinputmaturity\": -1}")] // deprecated
    [DataRow("{\"acceptnonstdconsolidationinput\": 1}")]
    [DataRow("{\"dustlimitfactor\": 301}")]
    [TestMethod]
    public async Task SubmitTransactionWithInvalidPolicy(string policy)
    {
      SetPoliciesForCurrentFeeQuote(policy);
      var (txHex, _) = CreateNewTransaction();

      var payloadSubmit = await SubmitTransactionAsync(txHex);
      Assert.AreEqual("failure", payloadSubmit.ReturnResult);
    }


    [TestMethod]
    public async Task SubmitAndQueryTransactionStatusWithAllValidPolicies()
    {
      SetPoliciesForCurrentFeeQuote(
        "{\"maxtxsizepolicy\" : 99999, \"datacarriersize\" : 100000, \"maxscriptsizepolicy\" : 100000, " +
        "\"maxscriptnumlengthpolicy\" : 100000, \"maxstackmemoryusagepolicy\" : 10000000, \"limitancestorcount\": 1000," +
        "\"limitcpfpgroupmemberscount\": 10, \"acceptnonstdoutputs\": true, \"datacarrier\": true, \"dustrelayfee\": 150, " +
        "\"maxstdtxvalidationduration\": 99, \"maxnonstdtxvalidationduration\": 100, \"minconsolidationfactor\": 10, " +
        "\"maxconsolidationinputscriptsize\": 100, \"minconfconsolidationinput\": 10, \"minconsolidationinputmaturity\": 10," +
        "\"acceptnonstdconsolidationinput\": false, \"dustlimitfactor\": 10 }");
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
        "\"limitcpfpgroupmemberscount\": 10, \"acceptnonstdoutputs\": true, \"datacarrier\": true, \"dustrelayfee\": 150, " +
        "\"maxstdtxvalidationduration\": 99, \"maxnonstdtxvalidationduration\": 100, \"minconsolidationfactor\": 10, " +
        "\"maxconsolidationinputscriptsize\": 100, \"minconfconsolidationinput\": 10, \"minconsolidationinputmaturity\": 10," +
        "\"acceptnonstdconsolidationinput\": false, \"dustlimitfactor\": 10 }");
      var payloadSubmit = await SubmitTransactionAsync(txHex);
      Assert.AreEqual("failure", payloadSubmit.ReturnResult);

      (txHex, _) = CreateNewTransaction();
      // invalid last dustlimitfactor - second (of duplicated) value is taken
      SetPoliciesForCurrentFeeQuote(
        "{\"maxtxsizepolicy\" : 99999, \"datacarriersize\" : 100000, \"maxscriptsizepolicy\" : 100000, " +
        "\"maxscriptnumlengthpolicy\" : 100000, \"maxstackmemoryusagepolicy\" : 10000000, \"limitancestorcount\": 1000," +
        "\"limitcpfpgroupmemberscount\": 10, \"acceptnonstdoutputs\": true, \"datacarrier\": true, \"dustrelayfee\": 150, " +
        "\"maxstdtxvalidationduration\": 99, \"maxnonstdtxvalidationduration\": 100, \"minconsolidationfactor\": 10, " +
        "\"maxconsolidationinputscriptsize\": 100, \"minconfconsolidationinput\": 10, \"minconsolidationinputmaturity\": 10," +
        "\"acceptnonstdconsolidationinput\": false, \"dustlimitfactor\": 10, \"dustlimitfactor\": -1 }");
      payloadSubmit = await SubmitTransactionAsync(txHex);
      Assert.AreEqual("failure", payloadSubmit.ReturnResult);

      (txHex, _) = CreateNewTransaction();
      // duplicated dustlimitfactor policy is not a problem
      SetPoliciesForCurrentFeeQuote(
        "{\"maxtxsizepolicy\" : 99999, \"datacarriersize\" : 100000, \"maxscriptsizepolicy\" : 100000, " +
        "\"maxscriptnumlengthpolicy\" : 100000, \"maxstackmemoryusagepolicy\" : 10000000, \"limitancestorcount\": 1000," +
        "\"limitcpfpgroupmemberscount\": 10, \"acceptnonstdoutputs\": true, \"datacarrier\": true, \"dustrelayfee\": 150, " +
        "\"maxstdtxvalidationduration\": 99, \"maxnonstdtxvalidationduration\": 100, \"minconsolidationfactor\": 10, " +
        "\"maxconsolidationinputscriptsize\": 100, \"minconfconsolidationinput\": 10, \"minconsolidationinputmaturity\": 10," +
        "\"acceptnonstdconsolidationinput\": false, \"dustlimitfactor\": 10, \"dustlimitfactor\": 20 }");
      payloadSubmit = await SubmitTransactionAsync(txHex);
      Assert.AreEqual("success", payloadSubmit.ReturnResult);
    }

    [TestMethod]
    public async Task SubmitTransactionWithIgnoredPolicy()
    {
      var (txHex, _) = CreateNewTransaction();

      // unknown policy is ignored, so we get success
      SetPoliciesForCurrentFeeQuote(
        "{\"unknownpolicy\" : 99999 }");
      var payloadSubmit = await SubmitTransactionAsync(txHex);
      Assert.AreEqual("success", payloadSubmit.ReturnResult);

      (txHex, _) = CreateNewTransaction();

      // test casing - we get success, because flag is not found
      SetPoliciesForCurrentFeeQuote(
        "{\"maxTxSizepolicy\" : -1 }");
      payloadSubmit = await SubmitTransactionAsync(txHex);
      Assert.AreEqual("success", payloadSubmit.ReturnResult);
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
      InsertFeeQuote(GetMockedIdentity);

      (string txHex, string _) = CreateNewTransaction();

      var payloadSubmit = await SubmitTransactionAsync(txHex);
      Assert.AreEqual("failure", payloadSubmit.ReturnResult);

      RestAuthentication = GetMockedIdentityBearerToken;
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
    public async Task SubmitTransactionsWithDustRelayFeePolicy()
    {
      SetPoliciesForCurrentFeeQuote("{\"dustrelayfee\": 10 }");

      var tx1 = CreateNewTransactionTx(100);
      // consider tx2 and tx3 output to be dust (value is lower than the cost of spending it at the DustRelayFee)
      var tx2 = CreateNewTransactionTx(1);
      var tx3 = CreateNewTransactionTx(10);

      var payloadSubmit = await SubmitTransactionsAsync(new string[] { tx1.ToHex(), tx2.ToHex(), tx3.ToHex() });
      Assert.AreEqual(2, payloadSubmit.FailureCount);
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

  }
}
