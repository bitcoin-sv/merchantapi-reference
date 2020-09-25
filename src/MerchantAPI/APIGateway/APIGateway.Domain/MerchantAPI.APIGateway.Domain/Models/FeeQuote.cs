// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MerchantAPI.APIGateway.Domain.Models
{
  public class FeeQuote : IValidatableObject
  {
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ValidFrom { get; set; }
    //public int QuoteExpiryMinutes { get; set; } // not in db
    public string Identity { get; set; }
    public string IdentityProvider { get; set; }

    [JsonPropertyName("fees")]
    public Fee[] Fees { get; set; }

    //[JsonPropertyName("expiry")]
    //public Expiry Expiry { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
      if (CreatedAt > ValidFrom)
      {
        yield return new ValidationResult("Check ValidFrom value - cannot be valid before created.");
      }
      if ((Identity != null && IdentityProvider == null) || (Identity == null && IdentityProvider != null))
      {
        yield return new ValidationResult("Must provide both (identity and identityProvider) or none. ");
      }
      if (Identity?.Trim() == "")
      {
        yield return new ValidationResult("Identity must contain at least one non-whitespace character.");
      }
      if (IdentityProvider?.Trim() == "")
      {
        yield return new ValidationResult("IdentityProvider must contain at least one non-whitespace character.");
      }
      if (Fees == null || Fees.Length == 0)
      {
        yield return new ValidationResult("Fees array with at least one fee is required. ");
      }
      else
      {
        HashSet<string> hs = new HashSet<string>();
        foreach (var fee in Fees)
        {
          if (hs.Contains(fee.FeeType))
          {
            yield return new ValidationResult($"Fees array contains duplicate Fee for FeeType { fee.FeeType }");
          }
          hs.Add(fee.FeeType);

          var results = fee.Validate(validationContext);
          foreach (var result in results)
          {
            yield return result;
          }
        }
      }

    }

  }
}
