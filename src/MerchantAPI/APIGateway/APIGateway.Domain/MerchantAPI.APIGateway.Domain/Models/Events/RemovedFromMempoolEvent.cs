// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.APIGateway.Domain.Models.Zmq;
using MerchantAPI.Common.EventBus;

namespace MerchantAPI.APIGateway.Domain.Models.Events
{
  /// <summary>
  /// Triggered when a we receive "removedfrommempool "from node via ZMQ
  /// </summary>
  public class RemovedFromMempoolEvent : IntegrationEvent
  {
    public RemovedFromMempoolEvent() : base()
    {
    }
    public RemovedFromMempoolMessage Message { get; set; }
  }
}
