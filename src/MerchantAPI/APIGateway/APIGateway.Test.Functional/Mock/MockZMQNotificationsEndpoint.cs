// Copyright(c) 2021 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain.Models;

namespace MerchantAPI.APIGateway.Test.Functional.Mock
{
  public class MockZMQNotificationsEndpoint : IZMQNotificationsEndpoint
  {
    public bool IsZMQNotificationsEndpointReachable(string ZMQNotificationsEndpoint)
    {
      return true;
    }
  }
}