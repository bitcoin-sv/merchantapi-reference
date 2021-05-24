// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System.Text.Json.Serialization;

namespace MerchantAPI.APIGateway.Domain.Models
{
  public class Expiry
  {
    [JsonPropertyName("feeOnExpiry")]
    public FeeAmount FeeOnExpiry { get; set; }

    [JsonPropertyName("keepInMempoolFee")]
    public FeeAmount KeepInMempoolFee { get; set; }

    [JsonPropertyName("mempoolExpiryFee")]
    public FeeAmount MempoolExpiryFee { get; set; }
  }
}
