// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain.Models;
using System.Text.Json.Serialization;

namespace MerchantAPI.APIGateway.Rest.ViewModels
{
  public class FeeAmountViewModelGet
  {
    [JsonPropertyName("satoshis")]
    public int Satoshis { get; set; }

    [JsonPropertyName("bytes")]
    public int Bytes { get; set; }

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
