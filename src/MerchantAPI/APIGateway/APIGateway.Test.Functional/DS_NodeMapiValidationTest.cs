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

      var tx1 = CreateDS_OP_RETURN_Tx(new Coin[] { coin }, 00);
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

    private Transaction CreateDS_Tx(Script script)
    {
      var coin = availableCoins.Dequeue();
      return CreateDS_Tx(new Coin[] { coin }, script);
    }

    [TestMethod]
    public async Task SubmitTxWithoutDsCheckAndMissingOP_FALSE()
    {
      //var script = new Script(OpcodeType.OP_FALSE);
      var script = new Script(OpcodeType.OP_RETURN);
      script += Op.GetPushOp(Encoders.Hex.DecodeData(DSNTidentifier));

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
      //var script = new Script(OpcodeType.OP_FALSE);
      var script = new Script(OpcodeType.OP_RETURN);
      script += Op.GetPushOp(Encoders.Hex.DecodeData(DSNTidentifier));
      
      string dsData = $"01017f000001{0:D2}";
      script += Op.GetPushOp(Encoders.Hex.DecodeData(dsData));

      var tx1 = CreateDS_Tx(script);

      var payload = await SubmitTransactions(new string[] { tx1.ToHex() });

      Assert.AreEqual("success", payload.Txs.Single().ReturnResult);
      Assert.AreEqual(1, payload.Txs.Single().Warnings.Length);
      Assert.AreEqual("DS not enabled.", payload.Txs.Single().Warnings.Single());
    }

    [TestMethod]
    public async Task SubmitTxWithMissingOP_RETURN()
    {
      var script = new Script(OpcodeType.OP_FALSE);
      //script += OpcodeType.OP_RETURN;
      script += Op.GetPushOp(Encoders.Hex.DecodeData(DSNTidentifier));

      string dsData = $"01017f000001{0:D2}";
      script += Op.GetPushOp(Encoders.Hex.DecodeData(dsData));

      var tx1 = CreateDS_Tx(script);

      var payload = await SubmitTransactions(new string[] { tx1.ToHex() });

      Assert.AreEqual("success", payload.Txs.Single().ReturnResult);
      Assert.AreEqual(1, payload.Txs.Single().Warnings.Length);
      Assert.AreEqual("DS not enabled.", payload.Txs.Single().Warnings.Single());
    }

    [TestMethod]
    public async Task SubmitTxWithMissingDSNTidentifier()
    {
      var script = new Script(OpcodeType.OP_FALSE);
      script += OpcodeType.OP_RETURN;
      //script += Op.GetPushOp(Encoders.Hex.DecodeData(DSNTidentifier));

      string dsData = $"01017f000001{0:D2}";
      script += Op.GetPushOp(Encoders.Hex.DecodeData(dsData));

      var tx1 = CreateDS_Tx(script);

      var payload = await SubmitTransactions(new string[] { tx1.ToHex() });

      Assert.AreEqual("success", payload.Txs.Single().ReturnResult);
      Assert.AreEqual(1, payload.Txs.Single().Warnings.Length);
      Assert.AreEqual("DS not enabled.", payload.Txs.Single().Warnings.Single());
    }

    [TestMethod]
    public async Task SubmitTxWithIncorrectDSNTidentifier()
    {
      var script = new Script(OpcodeType.OP_FALSE);
      script += OpcodeType.OP_RETURN;
      script += Op.GetPushOp(Encoders.Hex.DecodeData(DSNTidentifier+"01"));

      string dsData = $"01017f000001{0:D2}";
      script += Op.GetPushOp(Encoders.Hex.DecodeData(dsData));

      var tx1 = CreateDS_Tx(script);

      var payload = await SubmitTransactions(new string[] { tx1.ToHex() });

      Assert.AreEqual("success", payload.Txs.Single().ReturnResult);
      Assert.AreEqual(1, payload.Txs.Single().Warnings.Length);
      Assert.AreEqual("DS not enabled.", payload.Txs.Single().Warnings.Single());
    }

    [TestMethod]
    public async Task SubmitTxWithMissingCallbackMessage()
    {
      var script = new Script(OpcodeType.OP_FALSE);
      script += OpcodeType.OP_RETURN;
      script += Op.GetPushOp(Encoders.Hex.DecodeData(DSNTidentifier));

      //string dsData = $"01017f000001{0:D2}";
      //script += Op.GetPushOp(Encoders.Hex.DecodeData(dsData));

      var tx1 = CreateDS_Tx(script);

      var payload = await SubmitTransactions(new string[] { tx1.ToHex() });

      Assert.AreEqual("success", payload.Txs.Single().ReturnResult);
      Assert.AreEqual(1, payload.Txs.Single().Warnings.Length);
      Assert.AreEqual("Missing DSNT callback message.", payload.Txs.Single().Warnings.Single());
    }

    [TestMethod]
    public async Task SubmitTxWithInvalidProtocolVersion()
    {
      var script = new Script(OpcodeType.OP_FALSE);
      script += OpcodeType.OP_RETURN;
      script += Op.GetPushOp(Encoders.Hex.DecodeData(DSNTidentifier));

      string dsData = $"00017f000001{0:D2}"; // version 0
      script += Op.GetPushOp(Encoders.Hex.DecodeData(dsData));

      var tx1 = CreateDS_Tx(script);

      var payload = await SubmitTransactions(new string[] { tx1.ToHex() });

      Assert.AreEqual("success", payload.Txs.Single().ReturnResult);
      Assert.AreEqual(1, payload.Txs.Single().Warnings.Length);
      Assert.AreEqual("DSNT callback message: invalid version field.", payload.Txs.Single().Warnings.Single());
    }

    [TestMethod]
    public async Task SubmitTxWithInvalidVersionReservedBits()
    {
      var script = new Script(OpcodeType.OP_FALSE);
      script += OpcodeType.OP_RETURN;
      script += Op.GetPushOp(Encoders.Hex.DecodeData(DSNTidentifier));

      string dsData = $"ff017f000001{0:D2}"; // ff - reserved bits in version fields should be zero, but are not
      script += Op.GetPushOp(Encoders.Hex.DecodeData(dsData));

      var tx1 = CreateDS_Tx(script);

      var payload = await SubmitTransactions(new string[] { tx1.ToHex() });

      Assert.AreEqual("success", payload.Txs.Single().ReturnResult);
      Assert.AreEqual(1, payload.Txs.Single().Warnings.Length);
      Assert.AreEqual("DSNT callback message: invalid version field.", payload.Txs.Single().Warnings.Single());
    }

    [TestMethod]
    public async Task SubmitTxWithInvalidZeroIPAddressCount()
    {
      var script = new Script(OpcodeType.OP_FALSE);
      script += OpcodeType.OP_RETURN;
      script += Op.GetPushOp(Encoders.Hex.DecodeData(DSNTidentifier));

      string dsData = $"01007f000001{0:D2}"; // IP address count = 0
      script += Op.GetPushOp(Encoders.Hex.DecodeData(dsData));

      var tx1 = CreateDS_Tx(script);

      var payload = await SubmitTransactions(new string[] { tx1.ToHex() });

      Assert.AreEqual("success", payload.Txs.Single().ReturnResult);
      Assert.AreEqual(1, payload.Txs.Single().Warnings.Length);
      Assert.AreEqual("DSNT callback message: IP address count of 0 is not allowed.", payload.Txs.Single().Warnings.Single());
    }

    [TestMethod]
    public async Task SubmitTxWithInvalidIPAddressCount()
    {
      var script = new Script(OpcodeType.OP_FALSE);
      script += OpcodeType.OP_RETURN;
      script += Op.GetPushOp(Encoders.Hex.DecodeData(DSNTidentifier));

      string dsData = $"01037f0000017f000001{0:D2}"; // IP address count = 3
      script += Op.GetPushOp(Encoders.Hex.DecodeData(dsData));

      var tx1 = CreateDS_Tx(script);

      var payload = await SubmitTransactions(new string[] { tx1.ToHex() });

      Assert.AreEqual("success", payload.Txs.Single().ReturnResult);
      Assert.AreEqual(1, payload.Txs.Single().Warnings.Length);
      Assert.AreEqual("DSNT callback message: missing/bad IP address.", payload.Txs.Single().Warnings.Single());
    }

    [TestMethod]
    public async Task SubmitTxWithInvalidIPv6Address()
    {
      var script = new Script(OpcodeType.OP_FALSE);
      script += OpcodeType.OP_RETURN;
      script += Op.GetPushOp(Encoders.Hex.DecodeData(DSNTidentifier));

      string dsData = $"81037f0000017f000001{0:D2}"; // version 1 + 1 IPv6 address: 10000001 (hex: 81)
      script += Op.GetPushOp(Encoders.Hex.DecodeData(dsData));

      var tx1 = CreateDS_Tx(script);

      var payload = await SubmitTransactions(new string[] { tx1.ToHex() });

      Assert.AreEqual("success", payload.Txs.Single().ReturnResult);
      Assert.AreEqual(1, payload.Txs.Single().Warnings.Length);
      Assert.AreEqual("DSNT callback message: missing/bad IP address.", payload.Txs.Single().Warnings.Single());
    }

    [TestMethod]
    public async Task SubmitTxWithMissingInputCount()
    {
      var script = new Script(OpcodeType.OP_FALSE);
      script += OpcodeType.OP_RETURN;
      script += Op.GetPushOp(Encoders.Hex.DecodeData(DSNTidentifier));

      string dsData = $"01017f000001";
      script += Op.GetPushOp(Encoders.Hex.DecodeData(dsData));

      var tx1 = CreateDS_Tx(script);

      var payload = await SubmitTransactions(new string[] { tx1.ToHex() });

      Assert.AreEqual("success", payload.Txs.Single().ReturnResult);
      Assert.AreEqual(1, payload.Txs.Single().Warnings.Length);
      Assert.AreEqual("DSNT callback message: missing input count.", payload.Txs.Single().Warnings.Single());
    }

    [TestMethod]
    public async Task SubmitTxIPv6WithMissingInputCount()
    {
      var script = new Script(OpcodeType.OP_FALSE);
      script += OpcodeType.OP_RETURN;
      script += Op.GetPushOp(Encoders.Hex.DecodeData(DSNTidentifier));

      string dsData = $"8101{ new string('0', 31)}1";
      script += Op.GetPushOp(Encoders.Hex.DecodeData(dsData));

      var tx1 = CreateDS_Tx(script);

      var payload = await SubmitTransactions(new string[] { tx1.ToHex() });

      Assert.AreEqual("success", payload.Txs.Single().ReturnResult);
      Assert.AreEqual(1, payload.Txs.Single().Warnings.Length);
      Assert.AreEqual("DSNT callback message: missing input count.", payload.Txs.Single().Warnings.Single());
    }

    [TestMethod]
    public async Task SubmitTxWithInvalidInputCount()
    {
      var script = new Script(OpcodeType.OP_FALSE);
      script += OpcodeType.OP_RETURN;
      script += Op.GetPushOp(Encoders.Hex.DecodeData(DSNTidentifier));
      //      string dsData = $"8101{ new string('0', 31)}1{2:D2}";
      string dsData = $"01017f000001{2:D2}";
      script += Op.GetPushOp(Encoders.Hex.DecodeData(dsData));

      var tx1 = CreateDS_Tx(script);

      var payload = await SubmitTransactions(new string[] { tx1.ToHex() });

      Assert.AreEqual("success", payload.Txs.Single().ReturnResult);
      Assert.AreEqual(1, payload.Txs.Single().Warnings.Length);
      Assert.AreEqual("DSNT callback message: invalid input count.", payload.Txs.Single().Warnings.Single());
    }
  }
}
