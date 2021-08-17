// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using System.Linq;
using System.Text.Json;
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

    [JsonPropertyName("callbacks")]
    public CallbackViewModelGet[] Callbacks { get; set; }

    public FeeQuoteViewModelGet() { }

    public FeeQuoteViewModelGet(FeeQuote feeQuote, string[] urls)
    {
      ApiVersion = Const.MERCHANT_API_VERSION;
      Fees = (from fee in feeQuote.Fees
              select new FeeViewModelGet(fee)).ToArray();
      if (urls != null)
      {
        Callbacks = (from url in urls
                         select new CallbackViewModelGet(url)).ToArray();
      }
      // Other fields are initialized from BlockChainInfo and MinerId
    }

  }
}
