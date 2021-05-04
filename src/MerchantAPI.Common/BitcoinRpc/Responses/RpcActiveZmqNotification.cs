// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System.Text.Json.Serialization;

namespace MerchantAPI.Common.BitcoinRpc.Responses
{
  public class RpcActiveZmqNotification
  {
    [JsonPropertyName("notification")]
    public string Notification { get; set; }

    [JsonPropertyName("address")]
    public string Address { get; set; }
  }
}
