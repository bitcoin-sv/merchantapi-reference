// Copyright (c) 2020 Bitcoin Association

using System.Text.Json.Serialization;

namespace MerchantAPI.Common.BitcoinRpc.Responses
{
  public class RpcGetMerkleProof2
  {   
    [JsonPropertyName("index")]
    public long Index { get; set; }
    [JsonPropertyName("txOrId")]
    public string TxOrId { get; set; }
    [JsonPropertyName("targetType")]
    public string TargetType { get; set; }
    [JsonPropertyName("target")]
    public string Target { get; set; }
    [JsonPropertyName("nodes")]
    public string[] Nodes { get; set; }
  }
}
