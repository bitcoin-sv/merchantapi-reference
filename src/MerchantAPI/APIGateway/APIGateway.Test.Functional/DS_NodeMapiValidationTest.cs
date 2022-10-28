// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain;
using MerchantAPI.APIGateway.Rest.ViewModels;
using MerchantAPI.APIGateway.Test.Functional.Attributes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NBitcoin;
using NBitcoin.DataEncoders;
using System.Linq;
using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Test.Functional
{
  [TestCategory("TestCategoryNo2")]
  [TestClass]
  public class DS_NodeMapiValidationTest : DS_NodeMapiTestBase
  {
    private static string DsHexData => $"01017f000001{0:D2}"; // = version 1 + one Ipv4 localhost
    // see CreateDS_OP_RETURN_Script for details about script and hexData

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

    private static void AssertSuccessAndNoWarnings(SubmitTransactionsResponseViewModel payload, string txId = null)
    {
      var txResponse = string.IsNullOrEmpty(txId) ? payload.Txs.Single() : payload.Txs.Single(x => x.Txid == txId);
      Assert.AreEqual("success", txResponse.ReturnResult);
      Assert.AreEqual(0, txResponse.Warnings.Length);
      Assert.AreEqual(false, txResponse.FailureRetryable);
    }

    private static void AssertSuccessAndDSNTWarningPresent(SubmitTransactionsResponseViewModel payload, string txId = null)
    {
      var txResponse = string.IsNullOrEmpty(txId) ? payload.Txs.Single() :  payload.Txs.Single(x=> x.Txid==txId);
      Assert.AreEqual("success", txResponse.ReturnResult);
      Assert.AreEqual(1, txResponse.Warnings.Length);
      Assert.AreEqual(Warning.MissingDSNT, txResponse.Warnings.Single());
      Assert.AreEqual(false, txResponse.FailureRetryable);
    }

    private Task<SubmitTransactionsResponseViewModel> SubmitTransactionsWithDSAsync(string[] txHexList)
    {
      return SubmitTransactionsAsync(txHexList, true, false);
    }

    [TestMethod]
    public async Task SubmitTxWithDsCheck()
    {
      var coin = availableCoins.Dequeue();

      var tx1 = CreateDS_OP_RETURN_Tx(new Coin[] { coin }, DSprotectedInputs: 00);
      var tx1Hex = tx1.ToHex();

      var payload = await SubmitTransactionsWithDSAsync(new string[] { tx1Hex });

      AssertSuccessAndNoWarnings(payload);
    }

    [TestMethod]
    public async Task SubmitTxWithDsCheck_IPv6()
    {
      var coin = availableCoins.Dequeue();

      var tx1 = CreateDS_OP_RETURN_Tx(new Coin[] { coin }, IPv4: false, DSprotectedInputs: 00);
      var tx1Hex = tx1.ToHex();

      var payload = await SubmitTransactionsWithDSAsync(new string[] { tx1Hex });

      AssertSuccessAndNoWarnings(payload);
    }

    [TestMethod]
    public async Task SubmitTxWithDsCheck_MultipleAddresses()
    {
      var coin = availableCoins.Dequeue();

      var tx1 = CreateDS_OP_RETURN_Tx_IPAddressCount(new Coin[] { coin }, 10);
      var tx1Hex = tx1.ToHex();

      var payload = await SubmitTransactionsWithDSAsync(new string[] { tx1Hex });

      AssertSuccessAndNoWarnings(payload);
    }

    [TestMethod]
    public async Task SubmitTxWithoutDsCheck_MissingOP_FALSE()
    {
      var script = new Script(OpcodeType.OP_RETURN);
      script += Op.GetPushOp(Encoders.Hex.DecodeData(Const.DSNT_IDENTIFIER));

      string dsData = DsHexData;
      script += Op.GetPushOp(Encoders.Hex.DecodeData(dsData));

      var tx1 = CreateDS_Tx(script);

      var payload = await SubmitTransactionsAsync(new string[] { tx1.ToHex() }, dsCheck: false);

      // if we submit with DS disabled, there are no warnings
      AssertSuccessAndNoWarnings(payload);
      // if we resubmit we should get same response
      payload = await SubmitTransactionsAsync(new string[] { tx1.ToHex() }, dsCheck: false);
      AssertSuccessAndNoWarnings(payload);
    }

    [TestMethod]
    public async Task SubmitTxWithWithDsCheck_MissingOP_FALSE()
    {
      var script = new Script(OpcodeType.OP_RETURN);
      script += Op.GetPushOp(Encoders.Hex.DecodeData(Const.DSNT_IDENTIFIER));
      
      string dsData = DsHexData;
      script += Op.GetPushOp(Encoders.Hex.DecodeData(dsData));

      var tx1 = CreateDS_Tx(script);

      var payload = await SubmitTransactionsWithDSAsync(new string[] { tx1.ToHex() });

      AssertSuccessAndDSNTWarningPresent(payload);

      // on resubmit same warning must be present
      payload = await SubmitTransactionsWithDSAsync(new string[] { tx1.ToHex() });

      AssertSuccessAndDSNTWarningPresent(payload);
    }

    [OverrideSetting("AppSettings:ResubmitKnownTransactions", true)]
    [TestMethod]
    public async Task SubmitTxWithDsCheck_MissingOP_RETURN()
    {
      var script = new Script(OpcodeType.OP_FALSE);
      script += Op.GetPushOp(Encoders.Hex.DecodeData(Const.DSNT_IDENTIFIER));

      string dsData = DsHexData;
      script += Op.GetPushOp(Encoders.Hex.DecodeData(dsData));

      var tx1 = CreateDS_Tx(script);

      var payload = await SubmitTransactionsWithDSAsync(new string[] { tx1.ToHex() });

      AssertSuccessAndDSNTWarningPresent(payload);

      // on resubmit same warning must be present
      payload = await SubmitTransactionsWithDSAsync(new string[] { tx1.ToHex() });

      AssertSuccessAndDSNTWarningPresent(payload);
    }

    [TestMethod]
    public async Task SubmitTxWithDsCheck_MissingDSNTidentifier()
    {
      var script = new Script(OpcodeType.OP_FALSE);
      script += OpcodeType.OP_RETURN;

      string dsData = DsHexData;
      script += Op.GetPushOp(Encoders.Hex.DecodeData(dsData));

      var tx1 = CreateDS_Tx(script);

      var payload = await SubmitTransactionsWithDSAsync(new string[] { tx1.ToHex() });

      AssertSuccessAndDSNTWarningPresent(payload);
    }

    [TestMethod]
    public async Task SubmitTxWithDsCheck_IncorrectDSNTidentifier()
    {
      var script = new Script(OpcodeType.OP_FALSE);
      script += OpcodeType.OP_RETURN;
      script += Op.GetPushOp(Encoders.Hex.DecodeData(Const.DSNT_IDENTIFIER + "01"));

      string dsData = DsHexData;
      script += Op.GetPushOp(Encoders.Hex.DecodeData(dsData));

      var tx1 = CreateDS_Tx(script);

      var payload = await SubmitTransactionsWithDSAsync(new string[] { tx1.ToHex() });

      AssertSuccessAndDSNTWarningPresent(payload);
    }

    [TestMethod]
    public async Task SubmitTxWithDsCheck_MultipleDSNTOutputs()
    {
      var script1 = new Script(OpcodeType.OP_FALSE);
      script1 += OpcodeType.OP_RETURN;
      script1 += Op.GetPushOp(Encoders.Hex.DecodeData(Const.DSNT_IDENTIFIER));
      string dsData = DsHexData;
      script1 += Op.GetPushOp(Encoders.Hex.DecodeData(dsData));

      var coin = availableCoins.Dequeue();
      var tx1 = CreateDS_Tx(new Coin[] { coin }, new Script[] { script1, script1 });

      var payload = await SubmitTransactionsWithDSAsync(new string[] { tx1.ToHex() });

      Assert.AreEqual("success", payload.Txs.Single().ReturnResult);
      // There should only be one DSNT output in a transaction.
      // The node only attempts to process the first DSNT output (lowest index),
      // but this can change in the future, so we don't validate this at mAPI. 
      AssertSuccessAndNoWarnings(payload);
    }

    [TestMethod]
    public async Task SubmitTxsWithDsCheck_OneWarningReturned()
    {
      var script = new Script(OpcodeType.OP_FALSE);
      script += OpcodeType.OP_RETURN;
      script += Op.GetPushOp(Encoders.Hex.DecodeData(Const.DSNT_IDENTIFIER + "01"));

      string dsData = DsHexData;
      script += Op.GetPushOp(Encoders.Hex.DecodeData(dsData));

      var tx1 = CreateDS_Tx(script);
      var tx2 = CreateDS_OP_RETURN_Tx(new Coin[] { availableCoins.Dequeue() });

      var payload = await SubmitTransactionsWithDSAsync(new string[] { tx1.ToHex(), tx2.ToHex() });

      AssertSuccessAndDSNTWarningPresent(payload, tx1.GetHash().ToString());
      AssertSuccessAndNoWarnings(payload, tx2.GetHash().ToString());
    }
  }
}
