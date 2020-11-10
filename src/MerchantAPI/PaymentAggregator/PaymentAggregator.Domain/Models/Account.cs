// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MerchantAPI.PaymentAggregator.Domain.Models
{
  public class Account : IValidatableObject
  {
    public int AccountId { get; set; }
    public string OrganisationName { get; set; }
    public string ContactFirstName { get; set; }
    public string ContactLastName { get; set; }
    public string Email { get; set; }
    public string Identity { get; set; }
    public string IdentityProvider { get; set; }
    public DateTime CreatedAt { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
      if (!CommonValidator.IsEmailValid(Email))
      {
        yield return new ValidationResult($"{nameof(Email)}: { Email } is not valid.");
      }
    }
  }
}
