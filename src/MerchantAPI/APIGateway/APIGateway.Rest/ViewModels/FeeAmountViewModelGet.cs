// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.APIGateway.Domain.Models;
using System.Text.Json.Serialization;

namespace MerchantAPI.APIGateway.Rest.ViewModels
{
  public class FeeAmountViewModelGet
  {
    [JsonPropertyName("satoshis")]
    public long Satoshis { get; set; }

    [JsonPropertyName("bytes")]
    public long Bytes { get; set; }

    public FeeAmountViewModelGet() { }

    public FeeAmountViewModelGet(FeeAmount feeAmount)
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
