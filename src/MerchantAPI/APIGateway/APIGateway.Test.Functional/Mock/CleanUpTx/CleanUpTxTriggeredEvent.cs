// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.Common.EventBus;
using System;

namespace MerchantAPI.APIGateway.Test.Functional.CleanUpTx
{
  /// <summary>
  /// Triggered when we clean old transactions from database with CleanUpTxWithPauseHandlerForTest
  /// </summary>
  public class CleanUpTxTriggeredEvent : IntegrationEvent
  {
     public DateTime CleanUpTriggeredAt { get; set; }
  }
}
