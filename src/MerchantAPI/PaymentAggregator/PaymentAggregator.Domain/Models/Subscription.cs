// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MerchantAPI.PaymentAggregator.Domain.Models
{
  public class Subscription : IValidatableObject
  {
		public int SubscriptionId { get; set; }
		public int AccountID { get; set; }
		public string ServiceType { get; set; }
		public DateTime ValidFrom { get; set; }
		public DateTime? ValidTo { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
      if (!Consts.ServiceType.IsValid(ServiceType))
      {
        yield return new ValidationResult($"{ServiceType} is not a valid service type for a subscription.");
      }
    }
  }
}
