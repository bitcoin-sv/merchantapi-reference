// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.PaymentAggregator.Domain.Models;
using System;
using System.Text.Json.Serialization;

namespace MerchantAPI.PaymentAggregator.Rest.ViewModels
{
  public class SubscriptionViewModelGet
  {
		[JsonPropertyName("subscription_id")]
		public int SubscriptionId { get; set; }

		[JsonPropertyName("service")]
		public string ServiceType { get; set; }
		
		[JsonPropertyName("validFrom")]
		public DateTime ValidFrom { get; set; }
		
		[JsonPropertyName("validTo")]
		public DateTime? ValidTo { get; set; }

		public SubscriptionViewModelGet() { }

		public SubscriptionViewModelGet(Subscription subscription )
    {
			SubscriptionId = subscription.SubscriptionId;
			ServiceType = subscription.ServiceType;
			ValidFrom = subscription.ValidFrom;
			ValidTo = subscription.ValidTo;
    }
	}
}
