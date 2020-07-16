// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.APIGateway.Domain.Models;
using System;
using System.Linq;
using System.Text.Json.Serialization;

namespace MerchantAPI.APIGateway.Rest.ViewModels
{
  public class FeeQuoteViewModelCreate
  {
    [JsonIgnore]
    public long Id { get; set; }
    [JsonIgnore]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("validFrom")]
    public DateTime? ValidFrom { get; set; }

    [JsonPropertyName("identity")]
    public string Identity { get; set; }

    [JsonPropertyName("identityProvider")]
    public string IdentityProvider { get; set; }


    [JsonPropertyName("fees")]
    public FeeViewModelCreate[] Fees { get; set; }

    public FeeQuoteViewModelCreate() { }
    
    public FeeQuoteViewModelCreate(FeeQuote feeQuote)
    {
      Id = feeQuote.Id;
      CreatedAt = feeQuote.CreatedAt;
      ValidFrom = feeQuote.ValidFrom;
      Identity = feeQuote.Identity;
      IdentityProvider = feeQuote.IdentityProvider;
      Fees = (from fee in feeQuote.Fees
              select new FeeViewModelCreate(fee)).ToArray();
    }
    public FeeQuote ToDomainObject()
    {
      return new FeeQuote
      {
        CreatedAt = CreatedAt,
        ValidFrom = ValidFrom ?? DateTime.UtcNow, // can be null
        Identity = Identity,
        IdentityProvider = IdentityProvider,
        Fees = (Fees != null) ? (from fee in Fees
                                 select fee.ToDomainObject()).ToArray() : null
        };
    }
  }
}
