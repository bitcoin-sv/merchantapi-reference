// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System.Text.Json.Serialization;

namespace MerchantAPI.Common.BitcoinRpc.Responses
{
  public class RpcGetMerkleProof
  {
    [JsonPropertyName("flags")]
    public int Flags { get; set; }
    [JsonPropertyName("index")]
    public long Index { get; set; }
    [JsonPropertyName("txOrId")]
    public string TxOrId { get; set; }
    [JsonPropertyName("target")]
    public RpcGetBlockHeader Target { get; set; }
    [JsonPropertyName("nodes")]
    public string[]  Nodes { get; set; }
  }
}
