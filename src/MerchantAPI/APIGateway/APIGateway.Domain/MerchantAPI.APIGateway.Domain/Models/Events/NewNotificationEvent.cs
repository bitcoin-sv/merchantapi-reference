using MerchantAPI.Common.EventBus;
using System;
using System.Collections.Generic;
using System.Text;

namespace MerchantAPI.APIGateway.Domain.Models.Events
{
  public class NewNotificationEvent : IntegrationEvent
  {
    public string NotificationType { get; set; }
    public byte[] TransactionId { get; set; }
  }
}
