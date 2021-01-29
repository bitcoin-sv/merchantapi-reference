// Copyright (c) 2020 Bitcoin Association

using System;
using System.Linq;
using System.Text.Json.Serialization;
using MerchantAPI.PaymentAggregator.Domain.Models;

namespace MerchantAPI.PaymentAggregator.Rest.ViewModels
{
  public class FeeQuoteViewModelGet
  {
    [JsonPropertyName("apiVersion")]
    public string ApiVersion { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("expiryTime")]
    public DateTime ExpiryTime { get; set; }

    [JsonPropertyName("minerId")]
    public string MinerId { get; set; }

    [JsonPropertyName("currentHighestBlockHash")]
    public string CurrentHighestBlockHash { get; set; }

    [JsonPropertyName("currentHighestBlockHeight")]
    public long CurrentHighestBlockHeight { get; set; }

    [JsonPropertyName("fees")]
    public FeeViewModelGet[] Fees { get; set; }

    public FeeQuoteViewModelGet() { }

    public FeeQuoteViewModelGet(FeeQuote feeQuote)
    {
      Fees = (from fee in feeQuote.Fees
              select new FeeViewModelGet(fee)).ToArray();
    }
  }
}
