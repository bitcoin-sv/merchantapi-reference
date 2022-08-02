// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using Microsoft.VisualStudio.TestTools.UnitTesting;
using NBitcoin;
using NBitcoin.Altcoins;
using NBitcoin.DataEncoders;
using System.Linq;
using MerchantAPI.APIGateway.Domain;

namespace MerchantAPI.APIGateway.Test.Functional
{
  public class DS_NodeMapiTestBase : MapiWithBitcoindTestBase
  {
    [TestInitialize]
    public override void TestInitialize()
    {
      base.TestInitialize();

      InsertFeeQuote();
    }

    [TestCleanup]
    public override void TestCleanup()
    {
      base.TestCleanup();
    }

    protected static Transaction CreateDS_OP_RETURN_Tx(Coin[] coins, bool IPv4 = true, params int[] DSprotectedInputs)
    {
      return CreateDS_OP_RETURN_Tx(coins, IPv4, 1, DSprotectedInputs);
    }

    protected static Transaction CreateDS_OP_RETURN_Tx_IPAddressCount(Coin[] coins, int IPAddressCount, bool IPv4 = true)
    {
      return CreateDS_OP_RETURN_Tx(coins, IPv4, IPAddressCount, 00);
    }

    private static Transaction CreateDS_OP_RETURN_Tx(Coin[] coins, bool IPv4, int IPAddressCount, params int[] DSprotectedInputs)
    {
      var script = CreateDS_OP_RETURN_Script(IPv4, IPAddressCount, DSprotectedInputs);
      return CreateDS_Tx(coins, script);
    }

    private static Script CreateDS_OP_RETURN_Script(bool IPv4, int IPAddressCount, params int[] DSprotectedInputs)
    {
      // Callback details for a Double Spend Notification are embedded in an OP_RETURN output:
      // OP_FALSE OP_RETURN OP_PUSHDATA PROTOCOL_ID OP_PUSHDATA CALLBACK_MESSAGE

      var script = new Script(OpcodeType.OP_FALSE);
      script += OpcodeType.OP_RETURN;
      // PROTOCOL_ID: 32-bit identifier for Double Spend Notifications: 0x64736e74 "dsnt" (in this byte order)
      script += Op.GetPushOp(Encoders.Hex.DecodeData(Const.DSNT_IDENTIFIER));
      // The following hex data '01017f0000010100' is in accordance with specs where:
      // 1st byte (0x01) is version number
      // 2nd byte (0x01) is number of IPv4 addresses (in this case only 1)
      // next 4 bytes (0x7f000001) is the IP address for 127.0.0.1
      // next byte (0x01) is the number of input ids that will be listed for checking (in this case only 1)
      // last byte (0x00) is the input id we want to be checked (in this case it's the n=0)

      string versionByte = IPv4 ? "01" : "81"; // version 1 (first bit) + IPv6 address (last bit): 10000001 (hex: 81)
      // IP address count and input count are both of type varint - they can take 1-9 bytes
      NBitcoin.Protocol.VarInt vt = new((ulong)IPAddressCount);
      var IPaddressCountHex = Encoders.Hex.EncodeData(vt.ToBytes());
      string address = IPv4 ? "7f000001" : $"{ new string('0', 31)}1";
      var addresses = string.Concat(Enumerable.Repeat(address, IPAddressCount));

      string dsData = $"{versionByte}{IPaddressCountHex}{addresses}{DSprotectedInputs.Length:D2}";
      foreach (var input in DSprotectedInputs)
      {
        dsData += input.ToString("D2");
      }
      script += Op.GetPushOp(Encoders.Hex.DecodeData(dsData));

      return script;
    }

    protected Transaction CreateDS_Tx(Script script)
    {
      var coin = availableCoins.Dequeue();
      return CreateDS_Tx(coin, script);
    }

    protected Transaction CreateDS_Tx(Coin coin, Script script)
    {
      return CreateDS_Tx(new Coin[] { coin }, script);
    }

    protected static Transaction CreateDS_Tx(Coin[] coins, Script outputScript)
    {
      return CreateDS_Tx(coins, new Script[] { outputScript });
    }

    protected static Transaction CreateDS_Tx(Coin[] coins, Script[] outputScripts)
    {
      var address = BitcoinAddress.Create(testAddress, Network.RegTest);
      var tx1 = BCash.Instance.Regtest.CreateTransaction();

      foreach (var coin in coins)
      {
        tx1.Inputs.Add(new TxIn(coin.Outpoint));
        tx1.Outputs.Add(coin.Amount - new Money(1000L), address);
      }

      foreach (var script in outputScripts)
      {
        var txOut = new TxOut(new Money(0L), script);
        tx1.Outputs.Add(txOut);
      }

      var key = Key.Parse(testPrivateKeyWif, Network.RegTest);

      tx1.Sign(key.GetBitcoinSecret(Network.RegTest), coins);

      return tx1;
    }
  }
}
