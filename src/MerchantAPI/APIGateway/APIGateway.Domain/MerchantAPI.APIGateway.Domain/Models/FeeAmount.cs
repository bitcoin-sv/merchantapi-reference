// Copyright (c) 2020 Bitcoin Association

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MerchantAPI.APIGateway.Domain.Models
{
  public class FeeAmount : IValidatableObject
  {
    [JsonPropertyName("satoshis")]
    public long Satoshis { get; set; }

    [JsonPropertyName("bytes")]
    public long Bytes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
      if (Satoshis < 0 || Bytes < 0)
      {
        yield return new ValidationResult($"FeeAmount: value for {nameof(Satoshis)} and {nameof(Bytes)} must be non negative.");
      }
    }
  }
}
