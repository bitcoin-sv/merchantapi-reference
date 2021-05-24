// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain.Models.Zmq;
using MerchantAPI.Common.EventBus;

namespace MerchantAPI.APIGateway.Domain.Models.Events
{
  /// <summary>
  /// Triggered when a we receive "discardedfrommempool "from node via ZMQ
  /// </summary>
  public class RemovedFromMempoolEvent : IntegrationEvent
  {
    public RemovedFromMempoolEvent() : base()
    {
    }
    public RemovedFromMempoolMessage Message { get; set; }
  }
}
