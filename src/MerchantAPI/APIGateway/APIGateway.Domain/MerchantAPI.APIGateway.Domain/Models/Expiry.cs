// Copyright (c) 2020 Bitcoin Association

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
