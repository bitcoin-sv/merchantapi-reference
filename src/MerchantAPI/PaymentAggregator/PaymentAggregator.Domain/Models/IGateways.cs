// Copyright (c) 2020 Bitcoin Association

using System.Collections.Generic;
using System.Threading.Tasks;

namespace MerchantAPI.PaymentAggregator.Domain.Models
{
  public interface IGateways
  {
    Task<Gateway> CreateGatewayAsync(Gateway gateway);
    int DeleteGateway(int id);
    Gateway GetGateway(int id);
    IEnumerable<Gateway> GetGateways(bool onlyActive);
    Task<bool> UpdateGatewayAsync(Gateway gateway);
  }

}
