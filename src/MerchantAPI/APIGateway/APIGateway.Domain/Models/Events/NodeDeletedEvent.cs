// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.Common.EventBus;

namespace MerchantAPI.APIGateway.Domain.Models.Events
{
  public class NodeDeletedEvent : IntegrationEvent
  {
    public NodeDeletedEvent() : base()
    {
    }
    public Node DeletedNode { get; set; }
  }
}
