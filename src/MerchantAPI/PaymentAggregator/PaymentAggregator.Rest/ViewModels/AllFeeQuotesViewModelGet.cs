// Copyright (c) 2020 Bitcoin Association

using System.Text.Json.Serialization;

namespace MerchantAPI.PaymentAggregator.Rest.ViewModels
{
  public class AllFeeQuotesViewModelGet
  {
    [JsonPropertyName("miner")]
    public MinerFeeQuoteViewModelGet[] Miner { get; set; }

  }
}
