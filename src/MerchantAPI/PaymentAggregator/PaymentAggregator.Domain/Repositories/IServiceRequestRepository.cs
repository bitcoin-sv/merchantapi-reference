// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.PaymentAggregator.Domain.Models;
using System;
using System.Threading.Tasks;

namespace MerchantAPI.PaymentAggregator.Domain.Repositories
{
  public interface IServiceRequestRepository
  {
    Task<ServiceRequest[]> GetServiceRequestsAsync();
    Task<ServiceRequest> InsertServiceRequestAsync(ServiceRequest serviceRequest);
    Task CleanUpServiceRequestAsync(DateTime createdBefore);
  }
}
