// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.Common.EventBus;

namespace MerchantAPI.APIGateway.Domain.Models.Events
{
  /// <summary>
  /// This event is triggered from ZMQSubscriptionService when call to node rpc method activezmqnotifications fails.
  /// </summary>
  public class ZMQFailedEvent : IntegrationEvent
  {
    public ZMQFailedEvent() : base()
    {
    }
    public Node SourceNode { get; set; }
  }
}
