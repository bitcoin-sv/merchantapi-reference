// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.APIGateway.Domain.Models.Events;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Domain.Actions
{
  public interface INotificationAction
  {
    /// <summary>
    /// Get pending notifications and send them out to notifications service
    /// </summary>
    void ProcessAndSendNotifications();

    /// <summary>
    /// Process notifications from eventbus
    /// </summary>
    /// <param name="e">Event data</param>
    /// <returns></returns>
    Task SendNotificationFromEventAsync(NewNotificationEvent e);
  }
}
