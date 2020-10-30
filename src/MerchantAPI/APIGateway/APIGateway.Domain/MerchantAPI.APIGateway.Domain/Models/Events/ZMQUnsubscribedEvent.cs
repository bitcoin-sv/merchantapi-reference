// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.Common.EventBus;

namespace MerchantAPI.APIGateway.Domain.Models.Events
{
  /// <summary>
  /// This event is triggered from ZMQSubscriptionService when service unsubscribes from node zmq. Used mainly for tests.
  /// </summary>
  public class ZMQUnsubscribedEvent : IntegrationEvent
  {
    public ZMQUnsubscribedEvent() : base()
    {
    }
    public Node SourceNode { get; set; }
  }
}
