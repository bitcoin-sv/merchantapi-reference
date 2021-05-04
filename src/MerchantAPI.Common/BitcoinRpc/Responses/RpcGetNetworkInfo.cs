// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace MerchantAPI.Common.BitcoinRpc.Responses
{

  [Serializable]
  public class RpcGetNetworkInfo
  {
    [JsonPropertyName("minconsolidationfactor")]
    public long MinConsolidationFactor { get; set; }

    [JsonPropertyName("maxconsolidationinputscriptsize")]
    public long MaxConsolidationInputScriptSize { get; set; }

    [JsonPropertyName("minconsolidationinputmaturity")]
    public long MinConsolidationInputMaturity { get; set; }

    [JsonPropertyName("acceptnonstdconsolidationinput")]
    public bool AcceptNonStdConsolidationInput { get; set; }

    // There are additional fields that are not mapped


  }
}
