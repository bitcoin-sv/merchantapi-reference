// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System.Text.Json.Serialization;

namespace MerchantAPI.Common.BitcoinRpc.Responses
{
  public class RpcListUnspent
  {
    [JsonPropertyName("txid")]
    public string TxId { get; set; }

    [JsonPropertyName("vout")]
    public int Vout { get; set; }

    [JsonPropertyName("address")]
    public string Address { get; set; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("scriptPubKey")]
    public string ScriptPubKey { get; set; }

    public override string ToString()
    {
      return $"{TxId}, {Vout}, {Amount}";
    }

  }
}
