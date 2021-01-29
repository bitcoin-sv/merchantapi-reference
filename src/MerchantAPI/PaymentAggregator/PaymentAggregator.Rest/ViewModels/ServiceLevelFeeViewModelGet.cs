// Copyright (c) 2020 Bitcoin Association
using MerchantAPI.PaymentAggregator.Domain.Models;
using System.Text.Json.Serialization;

namespace MerchantAPI.PaymentAggregator.Rest.ViewModels
{
  public class ServiceLevelFeeViewModelGet
  {
    [JsonPropertyName("feeType")]
    public string FeeType { get; set; }

    [JsonPropertyName("miningFee")]
    public ServiceLevelFeeAmountViewModelGet MiningFee { get; set; }

    [JsonPropertyName("relayFee")]
    public ServiceLevelFeeAmountViewModelGet RelayFee { get; set; }

    public ServiceLevelFeeViewModelGet() { }

    public ServiceLevelFeeViewModelGet(Fee fee)
    {
      FeeType = fee.FeeType;
      MiningFee = new ServiceLevelFeeAmountViewModelGet(fee.MiningFee);
      RelayFee = new ServiceLevelFeeAmountViewModelGet(fee.RelayFee);
    }
  }
}
