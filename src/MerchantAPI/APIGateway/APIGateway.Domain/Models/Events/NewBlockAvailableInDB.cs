// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.Common.EventBus;

namespace MerchantAPI.APIGateway.Domain.Models.Events
{
  /// <summary>
  /// Triggered when a new block is inserted in database and is ready for parsing
  /// </summary>
  public class NewBlockAvailableInDB :  IntegrationEvent
  {
    public NewBlockAvailableInDB() : base()
    {
    }
    public string BlockHash { get; set; }
    public long BlockDBInternalId { get; set; }
    public long? BlockHeight { get; set; }
  }
}
