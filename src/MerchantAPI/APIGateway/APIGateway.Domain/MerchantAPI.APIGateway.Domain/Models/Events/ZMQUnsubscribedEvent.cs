// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.Common.EventBus;

namespace MerchantAPI.APIGateway.Domain.Models.Events
{
  public class ZMQUnsubscribedEvent : IntegrationEvent
  {
    public Node SourceNode { get; set; }
  }
}
