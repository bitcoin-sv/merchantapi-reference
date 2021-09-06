// Copyright(c) 2021 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

namespace MerchantAPI.APIGateway.Domain.Models
{
  public interface IZMQEndpointChecker
  {

    bool IsZMQNotificationsEndpointReachable(string ZMQNotificationsEndpoint);
  }
}
