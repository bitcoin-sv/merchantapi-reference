// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

namespace MerchantAPI.APIGateway.Test.SmokeTest
{
  public class UTXOCsv
  {
    public string TxId { get; set; }
    public int Vout { get; set; }
    public string Address { get; set; }
    public decimal Amount { get; set; }
    public string ScriptPubKey { get; set; }

    public UTXOCsv(string txId, int vout, string address, decimal amount, string scriptPubKey)
    {
      TxId = txId;
      Vout = vout;
      Address = address;
      Amount = amount;
      ScriptPubKey = scriptPubKey;
    }
  }
}
