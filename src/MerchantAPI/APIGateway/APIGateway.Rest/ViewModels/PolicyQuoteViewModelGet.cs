// Copyright(c) 2021 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain.Models;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MerchantAPI.APIGateway.Rest.ViewModels
{
  public class PolicyQuoteViewModelGet : FeeQuoteViewModelGet
  {
    [JsonPropertyName("policies")]
    public Dictionary<string, object> Policies { get; set; }

    public PolicyQuoteViewModelGet(FeeQuote feeQuote, string[] urls) : base(feeQuote, urls)
    {
      Policies = feeQuote.PoliciesDict;
      // Other fields are initialized from BlockChainInfo and MinerId
    }
  }
}
