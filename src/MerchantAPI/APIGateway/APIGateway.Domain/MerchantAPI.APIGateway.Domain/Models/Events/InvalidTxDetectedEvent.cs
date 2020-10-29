// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.APIGateway.Domain.Models.Zmq;
using MerchantAPI.Common.EventBus;

namespace MerchantAPI.APIGateway.Domain.Models.Events
{
  /// <summary>
  /// Triggered when a we receive invalid-tx message from node via ZMQ
  /// </summary>
  public class InvalidTxDetectedEvent : IntegrationEvent
  {
    public InvalidTxDetectedEvent() : base()
    {
    }

    public InvalidTxMessage Message { get; set; }
  }
}
