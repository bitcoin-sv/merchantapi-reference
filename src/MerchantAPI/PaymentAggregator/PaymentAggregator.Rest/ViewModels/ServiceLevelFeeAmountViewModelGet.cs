// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.Common.Domain.Models;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MerchantAPI.PaymentAggregator.Rest.ViewModels
{
  public class ServiceLevelFeeAmountViewModelGet
  {
    [Required]
    [JsonPropertyName("satoshis")]
    public int Satoshis { get; set; }

    [Required]
    [JsonPropertyName("bytes")]
    public int Bytes { get; set; }

    public ServiceLevelFeeAmountViewModelGet() { }

    public ServiceLevelFeeAmountViewModelGet(FeeAmount feeAmount)
    {
      Satoshis = feeAmount.Satoshis;
      Bytes = feeAmount.Bytes;
    }

    public FeeAmount ToDomainObject(string feeAmountType)
    {
      return
      new FeeAmount()
      {
        FeeAmountType = feeAmountType,
        Satoshis = Satoshis,
        Bytes = Bytes
      };
    }
  }
}
