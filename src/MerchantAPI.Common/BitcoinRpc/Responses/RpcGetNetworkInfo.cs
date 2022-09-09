// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using System.Text.Json.Serialization;

namespace MerchantAPI.Common.BitcoinRpc.Responses
{

  [Serializable]
  public class RpcGetNetworkInfo
  {
    [JsonPropertyName("version")]
    public long Version { get; set; }

    [JsonPropertyName("minconsolidationfactor")]
    public long MinConsolidationFactor { get; set; }

    [JsonPropertyName("maxconsolidationinputscriptsize")]
    public long MaxConsolidationInputScriptSize { get; set; }

    [JsonPropertyName("minconfconsolidationinput")]
    public long MinConfConsolidationInput { get; set; }

    [JsonPropertyName("acceptnonstdconsolidationinput")]
    public bool AcceptNonStdConsolidationInput { get; set; }

    [JsonPropertyName("warnings")]
    public string Warnings { get; set; }

    // There are additional fields that are not mapped


  }
}
