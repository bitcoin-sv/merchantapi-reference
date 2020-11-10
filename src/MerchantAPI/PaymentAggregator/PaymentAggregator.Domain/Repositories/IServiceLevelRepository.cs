// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.PaymentAggregator.Domain.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MerchantAPI.PaymentAggregator.Domain.Repositories
{
  public interface IServiceLevelRepository
  {
    IEnumerable<ServiceLevel> GetServiceLevels();
    Task<ServiceLevel[]> InsertServiceLevelsAsync(ServiceLevel[] serviceLevels);
  }
}
