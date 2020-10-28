// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.Common.EventBus;

namespace MerchantAPI.APIGateway.Domain.Models.Events
{
  public class NewNotificationEvent : IntegrationEvent
  {
    public string NotificationType { get; set; }
    public byte[] TransactionId { get; set; }
    public NotificationData NotificationData { get; set; }
  }
}
