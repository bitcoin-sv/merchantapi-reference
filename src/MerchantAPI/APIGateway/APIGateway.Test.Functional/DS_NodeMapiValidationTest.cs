// Copyright(c) 2021 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using Microsoft.VisualStudio.TestTools.UnitTesting;
using NBitcoin;
using NBitcoin.DataEncoders;
using System.Linq;
using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Test.Functional
{
  [TestClass]
  public class DS_NodeMapiValidationTest : DS_NodeMapiTestBase
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

    [TestMethod]
    public async Task SubmitTxWithDsCheckAndCorrectScript()
    {
      var coin = availableCoins.Dequeue();

      var tx1 = CreateDS_OP_RETURN_Tx(new Coin[] { coin }, DSprotectedInputs: 00);
      var tx1Hex = tx1.ToHex();

      var payload = await SubmitTransactions(new string[] { tx1Hex });

      Assert.AreEqual("success", payload.Txs.Single().ReturnResult);
      Assert.AreEqual(0, payload.Txs.Single().Warnings.Length);
    }

    [TestMethod]
    public async Task SubmitTxIPv6WithDsCheckAndCorrectScript()
    {
      var coin = availableCoins.Dequeue();

      var tx1 = CreateDS_OP_RETURN_Tx(new Coin[] { coin }, IPv4: false, DSprotectedInputs: 00);
      var tx1Hex = tx1.ToHex();

      var payload = await SubmitTransactions(new string[] { tx1Hex });

      Assert.AreEqual("success", payload.Txs.Single().ReturnResult);
      Assert.AreEqual(0, payload.Txs.Single().Warnings.Length);
    }

    [TestMethod]
    public async Task SubmitTxWithDsCheckAndCorrectScript_MultipleAddresses()
    {
      var coin = availableCoins.Dequeue();

      var tx1 = CreateDS_OP_RETURN_Tx_IPAddressCount(new Coin[] { coin }, 10);
      var tx1Hex = tx1.ToHex();

      var payload = await SubmitTransactions(new string[] { tx1Hex });

      Assert.AreEqual("success", payload.Txs.Single().ReturnResult);
      Assert.AreEqual(0, payload.Txs.Single().Warnings.Length);
    }

    [TestMethod]
    public async Task SubmitTxWithoutDsCheckAndMissingOP_FALSE()
    {
      var script = new Script(OpcodeType.OP_RETURN);
      script += Op.GetPushOp(Encoders.Hex.DecodeData(DSNTIdentifier));

      string dsData = $"01017f000001{0:D2}";
      script += Op.GetPushOp(Encoders.Hex.DecodeData(dsData));

      var tx1 = CreateDS_Tx(script);

      var payload = await SubmitTransactions(new string[] { tx1.ToHex() }, dsCheck: false);

      // if we submit with DS disabled, there are no warnings
      Assert.AreEqual("success", payload.Txs.Single().ReturnResult);
      Assert.AreEqual(0, payload.Txs.Single().Warnings.Length);
    }

    [TestMethod]
    public async Task SubmitTxWithMissingOP_FALSE()
    {
      var script = new Script(OpcodeType.OP_RETURN);
      script += Op.GetPushOp(Encoders.Hex.DecodeData(DSNTIdentifier));
      
      string dsData = $"01017f000001{0:D2}";
      script += Op.GetPushOp(Encoders.Hex.DecodeData(dsData));

      var tx1 = CreateDS_Tx(script);

      var payload = await SubmitTransactions(new string[] { tx1.ToHex() });

      Assert.AreEqual("success", payload.Txs.Single().ReturnResult);
      Assert.AreEqual(1, payload.Txs.Single().Warnings.Length);
      Assert.AreEqual("Missing DSNT output.", payload.Txs.Single().Warnings.Single());
    }

    [TestMethod]
    public async Task SubmitTxWithMissingOP_RETURN()
    {
      var script = new Script(OpcodeType.OP_FALSE);
      script += Op.GetPushOp(Encoders.Hex.DecodeData(DSNTIdentifier));

      string dsData = $"01017f000001{0:D2}";
      script += Op.GetPushOp(Encoders.Hex.DecodeData(dsData));

      var tx1 = CreateDS_Tx(script);

      var payload = await SubmitTransactions(new string[] { tx1.ToHex() });

      Assert.AreEqual("success", payload.Txs.Single().ReturnResult);
      Assert.AreEqual(1, payload.Txs.Single().Warnings.Length);
      Assert.AreEqual("Missing DSNT output.", payload.Txs.Single().Warnings.Single());
    }

    [TestMethod]
    public async Task SubmitTxWithMissingDSNTidentifier()
    {
      var script = new Script(OpcodeType.OP_FALSE);
      script += OpcodeType.OP_RETURN;

      string dsData = $"01017f000001{0:D2}";
      script += Op.GetPushOp(Encoders.Hex.DecodeData(dsData));

      var tx1 = CreateDS_Tx(script);

      var payload = await SubmitTransactions(new string[] { tx1.ToHex() });

      Assert.AreEqual("success", payload.Txs.Single().ReturnResult);
      Assert.AreEqual(1, payload.Txs.Single().Warnings.Length);
      Assert.AreEqual("Missing DSNT output.", payload.Txs.Single().Warnings.Single());
    }

    [TestMethod]
    public async Task SubmitTxWithIncorrectDSNTidentifier()
    {
      var script = new Script(OpcodeType.OP_FALSE);
      script += OpcodeType.OP_RETURN;
      script += Op.GetPushOp(Encoders.Hex.DecodeData(DSNTIdentifier+"01"));

      string dsData = $"01017f000001{0:D2}";
      script += Op.GetPushOp(Encoders.Hex.DecodeData(dsData));

      var tx1 = CreateDS_Tx(script);

      var payload = await SubmitTransactions(new string[] { tx1.ToHex() });

      Assert.AreEqual("success", payload.Txs.Single().ReturnResult);
      Assert.AreEqual(1, payload.Txs.Single().Warnings.Length);
      Assert.AreEqual("Missing DSNT output.", payload.Txs.Single().Warnings.Single());
    }

    [TestMethod]
    public async Task SubmitTxWithMultipleDSNTOutputs()
    {
      var script1 = new Script(OpcodeType.OP_FALSE);
      script1 += OpcodeType.OP_RETURN;
      script1 += Op.GetPushOp(Encoders.Hex.DecodeData(DSNTIdentifier));
      string dsData = $"01017f000001{0:D2}";
      script1 += Op.GetPushOp(Encoders.Hex.DecodeData(dsData));

      var coin = availableCoins.Dequeue();
      var tx1 = CreateDS_Tx(new Coin[] { coin }, new Script[] { script1, script1 });

      var payload = await SubmitTransactions(new string[] { tx1.ToHex() });

      Assert.AreEqual("success", payload.Txs.Single().ReturnResult);
      Assert.AreEqual(1, payload.Txs.Single().Warnings.Length);
      Assert.AreEqual("There should only be one DSNT output in a transaction. The node only attempts to process the first DSNT output (lowest index).", payload.Txs.Single().Warnings.Single());
    }
  }
}
