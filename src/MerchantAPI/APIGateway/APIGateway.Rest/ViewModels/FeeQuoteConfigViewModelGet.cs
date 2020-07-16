// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.APIGateway.Domain.Models;
using System;
using System.Linq;
using System.Text.Json.Serialization;

namespace MerchantAPI.APIGateway.Rest.ViewModels
{
  public class FeeQuoteConfigViewModelGet
  {

    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("validFrom")]
    public DateTime? ValidFrom { get; set; }

    [JsonPropertyName("identity")]
    public string Identity { get; set; }

    [JsonPropertyName("identityProvider")]
    public string IdentityProvider { get; set; }


    [JsonPropertyName("fees")]
    public FeeViewModelGet[] Fees { get; set; }

    public FeeQuoteConfigViewModelGet() { }

    public FeeQuoteConfigViewModelGet(FeeQuote feeQuote)
    {
      Id = feeQuote.Id;
      CreatedAt = feeQuote.CreatedAt;
      Identity = feeQuote.Identity;
      IdentityProvider = feeQuote.IdentityProvider;
      ValidFrom = feeQuote.ValidFrom;
      Fees = (from fee in feeQuote.Fees
              select new FeeViewModelGet(fee)).ToArray();
    }
  }
}
