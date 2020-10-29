// Copyright (c) 2020 Bitcoin Association

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
