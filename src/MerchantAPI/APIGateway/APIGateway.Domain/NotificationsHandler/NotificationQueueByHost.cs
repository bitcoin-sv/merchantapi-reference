// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain.Models;
using Microsoft.Extensions.Logging;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MerchantAPI.APIGateway.Domain.NotificationsHandler
{
  /// <summary>
  /// When accessing members of this class, the code must be in thread safe block
  /// </summary>
  public class NotificationQueueByHost
  {
    // Key represents host name
    readonly Dictionary<string, Queue<NotificationData>> notificationsQueue = new(StringComparer.InvariantCultureIgnoreCase);
    readonly ILogger logger;

    public NotificationQueueByHost(ILogger logger)
    {
      this.logger = logger;
    }

    public int GetNotificationCount(string[] hosts)
    {
      if (hosts == null || hosts.Length == 0)
      {
        return notificationsQueue.Sum(x => x.Value.Count);
      }

      int notificationCount = 0;
      foreach (var host in hosts)
      {
        if (notificationsQueue.TryGetValue(host, out var notifications))
        {
          notificationCount += notifications.Count;
        }
      }
      return notificationCount;
    }

    /// <summary>
    /// Add new notification to the queue for a given host
    /// </summary>
    public void AddNotification(string host, NotificationData notificationData)
    {
      if (notificationsQueue.TryGetValue(host, out var notifications))
      {
        notifications.Enqueue(notificationData);
        notificationsQueue[host] = notifications;
      }
      else
      {
        notifications = new Queue<NotificationData>();
        notifications.Enqueue(notificationData);
        notificationsQueue.Add(host, notifications);
      }
      logger.LogDebug($"Successfully enqueued notification for transaction '{new uint256(notificationData.TxExternalId)}'");
    }

    /// <summary>
    /// Return all waiting notifications for required host
    /// </summary>
    public Queue<NotificationData> GetWaitingNotifications(string host)
    {
      if (notificationsQueue.TryGetValue(host, out var waitingNotifications))
      {
        return waitingNotifications;
      }
      return null;
    }

    /// <summary>
    /// Remove the host from dictionary if it has no more notifications
    /// </summary>
    public void RemoveHostIfNoWork(string host)
    {
      notificationsQueue.Remove(host);
      logger.LogInformation($"Removing host '{host}' from queue since there are no more pending notifications after current dequeuing.");
    }
  }
}
