// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.APIGateway.Domain.Models;
using System.Text.Json.Serialization;

namespace MerchantAPI.APIGateway.Rest.ViewModels
{
  public class FeeViewModelCreate
  {
    [JsonPropertyName("feeType")]
    public string FeeType { get; set; }

    [JsonPropertyName("miningFee")]
    public FeeAmountViewModelCreate MiningFee { get; set; }

    [JsonPropertyName("relayFee")]
    public FeeAmountViewModelCreate RelayFee { get; set; }

    public FeeViewModelCreate() { }

    public FeeViewModelCreate(Fee fee)
    {
      FeeType = fee.FeeType;
      MiningFee = new FeeAmountViewModelCreate(fee.MiningFee);
      RelayFee = new FeeAmountViewModelCreate(fee.RelayFee);
    }

    public Fee ToDomainObject()
    {
      return new Fee
      {
        FeeType = FeeType,
        MiningFee = MiningFee?.ToDomainObject(FeeAmount.AmountType.MiningFee),
        RelayFee = RelayFee?.ToDomainObject(FeeAmount.AmountType.RelayFee)
      };
    }
  }
}
