// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.PaymentAggregator.Domain.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MerchantAPI.PaymentAggregator.Rest.ViewModels
{
  public class SubscriptionViewModelCreate
  {
    // Id property is required for unit tests
    [JsonIgnore]
    public int Id { get; set; }

    [JsonPropertyName("serviceType")]
    [Required]
    public string ServiceType { get; set; }

    public Subscription ToDomainModel()
    {
      return new Subscription
      {
        ServiceType = ServiceType,
        ValidFrom = DateTime.UtcNow
      };
    }
  }
}
