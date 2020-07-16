// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.Common.EventBus;

namespace MerchantAPI.APIGateway.Domain.Models.Events
{

  /// <summary>
  /// Triggered when a new block is detected. As response we insert basic block data in database.
  /// </summary>
  public class NewBlockDiscoveredEvent : IntegrationEvent
  {
    public string BlockHash { get; set;}
  }
}
