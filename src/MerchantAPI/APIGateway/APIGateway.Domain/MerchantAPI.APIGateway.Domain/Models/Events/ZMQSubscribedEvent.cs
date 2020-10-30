// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.Common.EventBus;

namespace MerchantAPI.APIGateway.Domain.Models.Events
{
  /// <summary>
  /// This event is triggered from ZMQSubscriptionService when service subscribes to nodes zmq.
  /// </summary>
  public class ZMQSubscribedEvent : IntegrationEvent
  {
    public ZMQSubscribedEvent() : base()
    {
    }
    public Node SourceNode { get; set; }
  }
}
