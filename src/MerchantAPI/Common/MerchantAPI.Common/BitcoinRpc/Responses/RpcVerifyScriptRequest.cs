// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using System.Text.Json.Serialization;

namespace MerchantAPI.Common.BitcoinRpc.Responses
{
  [Serializable]
  public class RpcVerifyScriptRequest
  {
    [JsonPropertyName("tx")]
    public string Tx { get; set; }

    [JsonPropertyName("n")]
    public int N { get; set; }

    [JsonPropertyName("flags")]
    public int? Flags { get; set; }

    [JsonPropertyName("reportflags")]
    public int? ReportFlags { get; set; }

    [JsonPropertyName("prevblockhash")]
    public string PrevBlockHash { get; set; }

    [JsonPropertyName("txo")]
    public RpcVerifyScriptTxo Txo { get; set; }
  }

  public class RpcVerifyScriptTxo
  {
    [JsonPropertyName("lock")]
    public string Lock { get; set; }

    [JsonPropertyName("value")]
    public int Value { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }
  }

  public class RpcVerifyScriptResponse
  {
    [JsonPropertyName("result")]
    public string Result { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonPropertyName("flags")]
    public string Flags { get; set; }
  }
}
