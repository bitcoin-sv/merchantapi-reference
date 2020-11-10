// Copyright (c) 2020 Bitcoin Association

using System;

namespace MerchantAPI.PaymentAggregator.Domain.Models
{
  public class ServiceRequest
  {
    public int ServiceRequestId { get; set; }
    public int SubscriptionId { get; set; }
    public DateTime Created { get; set; }
    public int ResponseCode { get; set; }
    public long ExecutionTimeMs { get; set; }
  }
}
