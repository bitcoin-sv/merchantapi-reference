// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

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
    public FeeQuote ToDomainObject(DateTime utcNow)
    {
      return new FeeQuote
      {
        CreatedAt = CreatedAt,
        ValidFrom = ValidFrom ?? utcNow, // can be null
        Identity = Identity,
        IdentityProvider = IdentityProvider,
        Fees = (Fees != null) ? (from fee in Fees
                                 select fee.ToDomainObject()).ToArray() : null
        };
    }
  }
}
