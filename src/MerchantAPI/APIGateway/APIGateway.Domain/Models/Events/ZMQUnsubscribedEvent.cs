// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

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
