// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.Common.Domain.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json.Serialization;

namespace MerchantAPI.PaymentAggregator.Domain.Models
{
  public class ServiceLevel : IValidatableObject
  {
    public long ServiceLevelId { get; set; }
    public int Level { get; set; }
    public string Description { get; set; }
    public DateTime? ValidTo { get; set; }
    public Fee[] Fees { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
      // only for highest level, Fees is null ...
      if (Fees != null) 
      {
        foreach (var fee in Fees)
        {
          if (fee == null)
          {
            yield return new ValidationResult($"ServiceLevel: { nameof(Fees) } array contains null value.");
          }
          else
          {
            if (Fees.Where(x => x?.FeeType == fee.FeeType).Count() > 1)
            {
              yield return new ValidationResult($"ServiceLevel: { nameof(Fees) } array contains duplicate Fee for FeeType { fee.FeeType }");
            }

            var results = fee.Validate(validationContext);
            foreach (var result in results)
            {
              yield return result;
            }
          }
        }
      }
      if (Level < 0)
      {
        yield return new ValidationResult($"ServiceLevel: value for { nameof(Level) } must be non negative. ");
      }
    }
  }
}
