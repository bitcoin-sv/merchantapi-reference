// Copyright (c) 2020 Bitcoin Association

using System;
using System.Linq;
using System.Text.Json.Serialization;
using MerchantAPI.APIGateway.Domain;
using MerchantAPI.APIGateway.Domain.Models;

namespace MerchantAPI.APIGateway.Rest.ViewModels
{
  public class FeeQuoteViewModelGet
  {
    // we don't send id

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
      ApiVersion = Const.MERCHANT_API_VERSION;
      Fees = (from fee in feeQuote.Fees
              select new FeeViewModelGet(fee)).ToArray();

      // Other fields are initialized from BlockCHainInfo and MinerId
    }

  }
}
