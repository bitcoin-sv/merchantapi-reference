// Copyright (c) 2020 Bitcoin Association

using System.Text.Json.Serialization;
using MerchantAPI.Common.Domain.Models;

namespace MerchantAPI.APIGateway.Rest.ViewModels
{
  public class FeeViewModelGet
  {
    [JsonPropertyName("feeType")]
    public string FeeType { get; set; }

    [JsonPropertyName("miningFee")]
    public FeeAmountViewModelGet MiningFee { get; set; }

    [JsonPropertyName("relayFee")]
    public FeeAmountViewModelGet RelayFee { get; set; }

    public FeeViewModelGet() { }

    public FeeViewModelGet(Fee fee)
    {
      FeeType = fee.FeeType;
      MiningFee = new FeeAmountViewModelGet(fee.MiningFee);
      RelayFee = new FeeAmountViewModelGet(fee.RelayFee);
    }
  }
}
