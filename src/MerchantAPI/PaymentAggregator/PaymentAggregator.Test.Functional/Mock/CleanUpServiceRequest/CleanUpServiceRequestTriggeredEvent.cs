// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.Common.EventBus;
using System;

namespace MerchantAPI.PaymentAggregator.Test.Functional.Mock.CleanUpServiceRequest
{
  /// <summary>
  /// Triggered when we clean old service requests from database with CleanUpServiceRequestWithPauseHandlerForTest
  /// </summary>
  public class CleanUpServiceRequestTriggeredEvent : IntegrationEvent
  {
    public DateTime CleanUpTriggeredAt { get; set; }
  }
}
