// Copyright(c) 2021 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using Microsoft.VisualStudio.TestTools.UnitTesting;
using NBitcoin;
using NBitcoin.Altcoins;
using NBitcoin.DataEncoders;
using System.Threading.Tasks;
using MerchantAPI.APIGateway.Rest.ViewModels;
using System.Net.Http;
using MerchantAPI.APIGateway.Domain.ViewModels;
using System.Net.Http.Headers;
using MerchantAPI.APIGateway.Test.Functional.Server;
using System.Net.Mime;
using System.Net;
using System.Linq;

namespace MerchantAPI.APIGateway.Test.Functional
{
  public class DS_NodeMapiTestBase: TestBaseWithBitcoind
  {
    protected const string DSNTidentifier = "64736e74";

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

    protected static Transaction CreateDS_OP_RETURN_Tx(Coin[] coins, params int[] DSprotectedInputs)
    {
      return CreateDS_OP_RETURN_Tx(coins, 1, DSprotectedInputs);
    }

    protected static Transaction CreateDS_OP_RETURN_Tx_IPAddressCount(Coin[] coins, int IPAddressCount)
    {
      return CreateDS_OP_RETURN_Tx(coins, IPAddressCount, 00);
    }

    private static Transaction CreateDS_OP_RETURN_Tx(Coin[] coins, int IPAddressCount, params int[] DSprotectedInputs)
    {
      // Callback details for a Double Spend Notification are embedded in an OP_RETURN output
      // OP_FALSE OP_RETURN OP_PUSHDATA PROTOCOL_ID OP_PUSHDATA CALLBACK_MESSAGE

      var script = new Script(OpcodeType.OP_FALSE);
      script += OpcodeType.OP_RETURN;
      // Add protocol id •	32-bit identifier for Double Spend Notifications: 0x64736e74 “dsnt” (in this byte order)
      script += Op.GetPushOp(Encoders.Hex.DecodeData(DSNTidentifier));
      // The following hex data '01017f0000010100' is in accordance with specs where:
      // 1st byte (0x01) is version number
      // 2nd byte (0x01) is number of IPv4 addresses (in this case only 1)
      // next 4 bytes (0x7f000001) is the IP address for 127.0.0.1
      // next byte (0x01) is the number of input ids that will be listed for checking (in this case only 1)
      // last byte (0x00) is the input id we want to be checked (in this case it's the n=0)

      // IP address count and input count are both of type varint - they can take 1-9 bytes
      NBitcoin.Protocol.VarInt vt = new((ulong)IPAddressCount);
      var IPaddressCountHex = Encoders.Hex.EncodeData(vt.ToBytes());

      var addresses = string.Concat(Enumerable.Repeat("7f000001", IPAddressCount));
      string dsData = $"01{IPaddressCountHex}{addresses}{DSprotectedInputs.Length:D2}";
      foreach (var input in DSprotectedInputs)
      {
        dsData += input.ToString("D2");
      }
      script += Op.GetPushOp(Encoders.Hex.DecodeData(dsData));

      return CreateDS_Tx(coins, script);
    }

    protected static Transaction CreateDS_Tx(Coin[] coins, Script script)
    {
      var address = BitcoinAddress.Create(testAddress, Network.RegTest);
      var tx1 = BCash.Instance.Regtest.CreateTransaction();

      foreach (var coin in coins)
      {
        tx1.Inputs.Add(new TxIn(coin.Outpoint));
        tx1.Outputs.Add(coin.Amount - new Money(1000L), address);
      }

      var txOut = new TxOut(new Money(0L), script);
      tx1.Outputs.Add(txOut);

      var key = Key.Parse(testPrivateKeyWif, Network.RegTest);

      tx1.Sign(key.GetBitcoinSecret(Network.RegTest), coins);

      return tx1;
    }

    protected async Task<SubmitTransactionsResponseViewModel> SubmitTransactions(string[] txHexList, bool dsCheck = true)
    {
      // Send transaction

      var reqJSON = "[{\"rawtx\": \"" + string.Join("\"}, {\"rawtx\": \"", txHexList) + "\", \"dscheck\": "+ dsCheck.ToString().ToLower() +", \"callbackurl\": \"http://mockCallback:8321\"}]";
      var reqContent = new StringContent(reqJSON);
      reqContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);

      var response =
        await Post<SignedPayloadViewModel>(MapiServer.ApiMapiSubmitTransactions, Client, reqContent, HttpStatusCode.OK);

      return response.response.ExtractPayload<SubmitTransactionsResponseViewModel>();
    }
  }
}
