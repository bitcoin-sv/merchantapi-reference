// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.PaymentAggregator.Consts;
using MerchantAPI.PaymentAggregator.Domain.Models;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MerchantAPI.PaymentAggregator.Rest.ViewModels
{
  public class ServiceLevelFeeViewModelCreate
  {
    [JsonIgnore]
    public long Id { get; set; }

    [Required]
    [JsonPropertyName("feeType")]
    public string FeeType { get; set; }

    [Required]
    [JsonPropertyName("miningFee")]
    public ServiceLevelFeeAmountViewModelCreate ServiceLevelMiningFeeAmount { get; set; }

    [Required]
    [JsonPropertyName("relayFee")]
    public ServiceLevelFeeAmountViewModelCreate ServiceLevelRelayFeeAmount { get; set; }

    public ServiceLevelFeeViewModelCreate()
    {

    }

    public ServiceLevelFeeViewModelCreate(Fee serviceLevelFee)
    {
      Id = Id;
      FeeType = FeeType;
      ServiceLevelMiningFeeAmount = new ServiceLevelFeeAmountViewModelCreate(serviceLevelFee.MiningFee);
      ServiceLevelRelayFeeAmount = new ServiceLevelFeeAmountViewModelCreate(serviceLevelFee.RelayFee);
    }

    public Fee ToDomainObject()
    {
      return new Fee
      {
        Id = Id,
        FeeType = FeeType,
        MiningFee = ServiceLevelMiningFeeAmount?.ToDomainObject(Const.AmountType.MiningFee),
        RelayFee = ServiceLevelRelayFeeAmount?.ToDomainObject(Const.AmountType.RelayFee)
      };
    }
  }
}
