// Copyright (c) 2020 Bitcoin Association

using System;
using System.Text.Json.Serialization;

namespace MerchantAPI.Common.BitcoinRpc.Responses
{
  [Serializable]
  public partial class RpcGetRawTransaction
  {
    [JsonPropertyName("txid")]
    public string Txid { get; set; }

    // Not all fields are mapped 

    //[JsonPropertyName("hash")]
    //public string Hash { get; set; }

    //[JsonPropertyName("version")]
    //public long Version { get; set; }

    //[JsonPropertyName("size")]
    //public long Size { get; set; }

    //[JsonPropertyName("locktime")]
    //public long Locktime { get; set; }

    [JsonPropertyName("vin")]
    public Vin[] Vin { get; set; }

    [JsonPropertyName("vout")]
    public Vout[] Vout { get; set; }

    [JsonPropertyName("blockhash")]
    public string Blockhash { get; set; }

    [JsonPropertyName("confirmations")]
    public long? Confirmations { get; set; }

    //[JsonPropertyName("time")]
    //public long Time { get; set; }

    [JsonPropertyName("blocktime")]
    public long? Blocktime { get; set; }

    [JsonPropertyName("blockheight")]
    public long? Blockheight { get; set; }

    //[JsonPropertyName("hex")]
    //public string Hex { get; set; }
  }

  [Serializable]
  public partial class Vin
  {
    [JsonPropertyName("coinbase")]
    public string Coinbase { get; set; }
    
    [JsonPropertyName("txid")]
    public string Txid { get; set; }

    [JsonPropertyName("vout")]
    public long Vout { get; set; }

    //[JsonPropertyName("scriptSig")]
    //public ScriptSig ScriptSig { get; set; }

    //[JsonPropertyName("sequence")]
    //public long Sequence { get; set; }
  }

  public partial class ScriptSig
  {
    [JsonPropertyName("asm")]
    public string Asm { get; set; }

    [JsonPropertyName("hex")]
    public string Hex { get; set; }
  }

  [Serializable]
  public partial class Vout
  {
    [JsonPropertyName("value")]
    public decimal Value { get; set; }

    [JsonPropertyName("n")]
    public long N { get; set; }

    [JsonPropertyName("scriptPubKey")]
    public ScriptPubKey ScriptPubKey { get; set; }
  }

  [Serializable]
  public partial class ScriptPubKey
  {
    //[JsonPropertyName("asm")]
    //public string Asm { get; set; }

    [JsonPropertyName("hex")]
    public string Hex { get; set; }

    //[JsonPropertyName("reqSigs")]
    //public long ReqSigs { get; set; }

    //[JsonPropertyName("type")]
    //public string Type { get; set; }

    //[JsonPropertyName("addresses")]
    //public string[] Addresses { get; set; }
  }
}
