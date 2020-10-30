using MerchantAPI.Common.EventBus;
using System;
using System.Collections.Generic;
using System.Text;

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
