// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.Common.EventBus;

namespace MerchantAPI.APIGateway.Domain.Models.Events
{
  public class NewNotificationEvent : IntegrationEvent
  {
    public NewNotificationEvent() : base()
    {
    }
    public string NotificationType { get; set; }
    public byte[] TransactionId { get; set; }
    public NotificationData NotificationData { get; set; }
  }
}
