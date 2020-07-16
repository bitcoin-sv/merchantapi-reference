// Copyright (c) 2020 Bitcoin Association

using System;
using System.Text.Json.Serialization;

namespace MerchantAPI.Common.BitcoinRpc.Responses
{
  [Serializable]
  public partial class RpcGetBlockHeader
  {
    [JsonPropertyName("hash")]
    public string Hash { get; set; }

    [JsonPropertyName("confirmations")]
    public long Confirmations { get; set; }

    [JsonPropertyName("height")]
    public long Height { get; set; }

    [JsonPropertyName("version")]
    public long Version { get; set; }

    [JsonPropertyName("versionHex")]
    public string VersionHex { get; set; }

    [JsonPropertyName("merkleroot")]
    public string Merkleroot { get; set; }

    [JsonPropertyName("num_tx")]
    public long NumTx { get; set; }

    [JsonPropertyName("time")]
    public long Time { get; set; }

    [JsonPropertyName("mediantime")]
    public long Mediantime { get; set; }

    [JsonPropertyName("nonce")]
    public long Nonce { get; set; }

    [JsonPropertyName("bits")]
    public string Bits { get; set; }

    [JsonPropertyName("difficulty")]
    public double Difficulty { get; set; }

    [JsonPropertyName("chainwork")]
    public string Chainwork { get; set; }

    [JsonPropertyName("previousblockhash")]
    public string Previousblockhash { get; set; }
  }
}
