// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MerchantAPI.Common.BitcoinRpc.Responses
{
  public class RpcGetMempoolAncestors
  {
    public Dictionary<string, RpcGetMempoolAncestor> Transactions { get; set; }
  }

  public class RpcGetMempoolAncestor
  {
    // hex of unconfirmed transactions used as inputs for this transaction
    [JsonPropertyName("depends")]
    public string[] Depends { get; set; }

    // There are additional fields that are not mapped
  }
}
