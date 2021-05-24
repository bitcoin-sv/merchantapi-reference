// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.Common.EventBus;

namespace MerchantAPI.APIGateway.Domain.Models.Events
{
  public class NodeAddedEvent : IntegrationEvent
  {
    public NodeAddedEvent() : base()
    {
    }
    public Node CreatedNode { get; set; }
  }
}
