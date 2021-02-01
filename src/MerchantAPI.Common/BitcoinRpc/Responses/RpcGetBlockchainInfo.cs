// Copyright (c) 2020 Bitcoin Association

using System;
using System.Text.Json.Serialization;

namespace MerchantAPI.Common.BitcoinRpc.Responses
{

  [Serializable]
  public class RpcGetBlockchainInfo
  {
    [JsonPropertyName("chain")]
    public string Chain { get; set; }

    [JsonPropertyName("blocks")]
    public long Blocks { get; set; }

    [JsonPropertyName("headers")]
    public long Headers{ get; set; }

    [JsonPropertyName("bestblockhash")]
    public string BestBlockHash { get; set; }

    // There are additional fields that are not mapped


  }
}
