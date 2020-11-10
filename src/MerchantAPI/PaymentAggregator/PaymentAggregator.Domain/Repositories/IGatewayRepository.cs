// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.PaymentAggregator.Domain.Models;
using System.Collections.Generic;

namespace MerchantAPI.PaymentAggregator.Domain.Repositories
{
  public interface IGatewayRepository
  {
    /// <summary>
    /// Returns null if gateway already exists
    /// </summary>
    Gateway CreateGateway(Gateway gateway);


    /// <summary>
    /// Returns false if not found, Can not be used to update GatewayStatus
    /// </summary>
    bool UpdateGateway(Gateway gateway);

    /// <summary>
    /// Updates lastError and lastErrorAt fields
    /// </summary>
    /// <returns>false if not updated</returns>
    bool UpdateGatewayError(Gateway gateway);

    Gateway GetGateway(int id);

    int DeleteGateway(int id);

    public IEnumerable<Gateway> GetGateways(bool onlyActive=false);
  }

}
