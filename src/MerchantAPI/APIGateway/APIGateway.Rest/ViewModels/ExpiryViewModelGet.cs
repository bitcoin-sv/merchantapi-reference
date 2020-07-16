// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.APIGateway.Domain.Models;
using System.Text.Json.Serialization;

namespace MerchantAPI.APIGateway.Rest.ViewModels
{
  public class ExpiryViewModelGet
  {

    [JsonPropertyName("feeOnExpiry")]
    public FeeAmountViewModelGet FeeOnExpiry { get; set; }

    [JsonPropertyName("keepInMempoolFee")]
    public FeeAmountViewModelGet KeepInMempoolFee { get; set; }

    [JsonPropertyName("mempoolExpiryFee")]
    public FeeAmountViewModelGet MempoolExpiryFee { get; set; }


    public ExpiryViewModelGet() { }

    public ExpiryViewModelGet(Expiry expiry)
    {
      FeeOnExpiry = new FeeAmountViewModelGet(expiry.FeeOnExpiry);
      KeepInMempoolFee = new FeeAmountViewModelGet(expiry.KeepInMempoolFee);
      MempoolExpiryFee = new FeeAmountViewModelGet(expiry.MempoolExpiryFee);
    }

  }
}
