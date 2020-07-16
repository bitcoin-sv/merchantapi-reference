// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MerchantAPI.APIGateway.Domain.Models
{
  public class Fee: IValidatableObject
  {
    public long Id { get; set; }
    [JsonPropertyName("feeType")]
    public string FeeType { get; set; }

    [JsonPropertyName("miningFee")]
    public FeeAmount MiningFee { get; set; }

    [JsonPropertyName("relayFee")]
    public FeeAmount RelayFee { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
      if (String.IsNullOrEmpty(FeeType))
      {
        yield return new ValidationResult($"Fee: value for {nameof(FeeType)} must not be null or empty.");
      }
      if (MiningFee == null)
      {
        yield return new ValidationResult($"Fee: null value for {nameof(MiningFee)} is invalid.");
      }
      else
      {
        foreach (var result in MiningFee.Validate(validationContext))
        {
          yield return result;
        }
      }

      if (RelayFee == null)
      {
        yield return new ValidationResult($"Fee: null value for {nameof(RelayFee)} is invalid.");
      }
      else
      {
        foreach (var result in RelayFee.Validate(validationContext))
        {
          yield return result;
        }
      }

    }
  }

}
