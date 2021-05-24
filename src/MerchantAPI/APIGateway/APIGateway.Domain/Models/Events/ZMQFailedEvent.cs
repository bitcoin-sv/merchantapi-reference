// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

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
