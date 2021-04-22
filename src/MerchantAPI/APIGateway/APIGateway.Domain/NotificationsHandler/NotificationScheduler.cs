// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain.Models;
using Microsoft.Extensions.Logging;
using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Domain.NotificationsHandler
{
  public class NotificationScheduler
  {
    readonly object lockingObject = new object();

    readonly HostExecutionTimes hostExecutionTimes;

    // The slow/fast queues contains unique hosts for which we have some notifications in notificationQueue. 
    // Hosts are ordered by notification arrival time. When collecting data for callback, we consume up to 
    // maxNotificationsInBatch notifications. If there are any left, we move the the hosts to the end 
    // of the queue to give other hosts chance to get their notifications
    readonly Queue<string> slowWaitingHosts = new Queue<string>();
    readonly Queue<string> fastWaitingHosts = new Queue<string>();

    // Queues of waiting tasks, upon releasing of the task, the task will receive list of notifications that must be sent
    readonly Queue<TaskCompletionSource<List<NotificationData>>> slowWaitingQueue = new Queue<TaskCompletionSource<List<NotificationData>>>();
    readonly Queue<TaskCompletionSource<List<NotificationData>>> fastWaitingQueue = new Queue<TaskCompletionSource<List<NotificationData>>>();

    readonly NotificationQueueByHost notificationsQueue;

    readonly int maxNumberOfSlowNotifications;
    readonly int maxInstantNotificationsQueueSize;
    readonly int maxNotificationsInBatch;
    readonly ILogger logger;

    public NotificationScheduler(ILogger logger, int maxNumberOfSlowNotifications, int maxInstantNotificationsQueueSize,
                                      int maxNotificationsInBatch, int maxNoOfSavedExecutionTimes, int slowHttpClientThresholdInMs)
    {
      this.maxNumberOfSlowNotifications = maxNumberOfSlowNotifications;
      this.maxInstantNotificationsQueueSize = maxInstantNotificationsQueueSize;
      this.maxNotificationsInBatch = maxNotificationsInBatch;
      this.logger = logger;
      notificationsQueue = new NotificationQueueByHost(logger);
      hostExecutionTimes = new HostExecutionTimes(maxNoOfSavedExecutionTimes, slowHttpClientThresholdInMs);

    }

    private List<NotificationData> GetNextBatchOfNotificationsNL(string host, out bool moreNotificationsAvailable)
    {
      moreNotificationsAvailable = false;
      var scheduledNotifications = new List<NotificationData>();
      var waitingNotifications = notificationsQueue.GetWaitingNotifications(host);

      if (waitingNotifications == null)
      {
        // No work available for current worker
        logger.LogWarning($"This should not happen!!! Queue is not empty, but no notifications are available for host '{host}'");
      }
      else
      {
        // Prepare a batch of notifications to be handed over to the HTTPClient
        do
        {
          scheduledNotifications.Add(waitingNotifications.Dequeue());
          moreNotificationsAvailable = waitingNotifications.Count > 0;
        }
        while (scheduledNotifications.Count < maxNotificationsInBatch && moreNotificationsAvailable);
      }

      logger.LogInformation($"Successfully dequeued {scheduledNotifications.Count} notifications for host '{host}'");
      return scheduledNotifications;
    }

    private void ReleaseTaskOrAddHostToQueueNL(string host, Queue<string> hosts, Queue<TaskCompletionSource<List<NotificationData>>> tasksQueue) 
    {
      host = host.ToLowerInvariant();
      if (hosts.Any(x => x == host))
      {
        return;
      }
      hosts.Enqueue(host);

      if (tasksQueue.TryDequeue(out var tcs))
      {
        var nextHost = hosts.Dequeue();

        tcs.TrySetResult(GetNextBatchOfNotificationsNL(nextHost, out bool moreNotificationsAvailable));
        // If the host was not removed from the notifications queue, this means there are still pending notifications
        // and we must enqueue the host again for more processing
        if (moreNotificationsAvailable)
        {
          hosts.Enqueue(nextHost);
        }
        else
        {
          notificationsQueue.RemoveHostIfNoWork(nextHost);
        }
      }
    }

    private Task<List<NotificationData>> GetNextTaskAsync(Queue<string> hostsQueue, Queue<TaskCompletionSource<List<NotificationData>>> tasksQueue, CancellationToken stoppingToken)
    {
      lock (lockingObject)
      {
        if (hostsQueue.TryDequeue(out var nextHost))
        {
          var nextTask = Task.FromResult(GetNextBatchOfNotificationsNL(nextHost, out bool moreNotificationsAvailable));
          // If the host was not removed from the notifications queue, this means there are still pending notifications
          // and we must enqueue the host again for more processing
          if (moreNotificationsAvailable)
          {
            hostsQueue.Enqueue(nextHost);
          }
          else
          {
            notificationsQueue.RemoveHostIfNoWork(nextHost);
          }

          return nextTask;
        }
        else
        {
          // TaskCreationOptions.RunContinuationsAsynchronously is used to release tasks on new threads instead of letting them run on same thread 
          // that released a new task
          var tcs = new TaskCompletionSource<List<NotificationData>>(TaskCreationOptions.RunContinuationsAsynchronously);
          tasksQueue.Enqueue(tcs);
          return tcs.Task.WithCancellation(stoppingToken);
        }
      }
    }

    private bool TooManyNotificationsNL(string[] slowHosts, bool slowHostNotification)
    {
      // Count how many slow notifications are waiting to be sent out
      var slowNotificationCount = notificationsQueue.GetNotificationCount(slowHosts);
      var tooManySlowNotifications = slowHostNotification &&
                                      slowNotificationCount >= maxNumberOfSlowNotifications;
      // Queue for instant notifications is full
      // or the notification belongs to host that is known as slow host and we already have too many slow hosts waiting so
      // we will mark the notification for background job instead
      if (tooManySlowNotifications ||
          notificationsQueue.GetNotificationCount(null) >= maxInstantNotificationsQueueSize)
      {
        if (tooManySlowNotifications)
        {
          logger.LogWarning("Too many slow notifications in queue, marking notification to be processed by background thread.");
        }
        else
        {
          logger.LogWarning("Notifications queue is full, marking notification to be processed by background thread.");
        }

        return true;
      }

      return false;
    }

    /// <summary>
    /// Called when new notification is available for a host. If this host is already in waiting list 
    /// then do nothing, otherwise release next task in waiting in queue with host that was first in 
    /// the queue.
    /// If too many notifications are already present, then this notification will not be inserted
    /// </summary>
    public bool Add(NotificationData notification, string host)
    {
      lock (lockingObject)
      {
        var slowHosts = hostExecutionTimes.GetHosts(true);
        bool isSlowHost = slowHosts != null && slowHosts.Any(x => x == host);
        if (TooManyNotificationsNL(slowHosts, isSlowHost))
        {
          return false;
        }
        notificationsQueue.AddNotification(host, notification);
        if (isSlowHost)
        {
          ReleaseTaskOrAddHostToQueueNL(host.ToLowerInvariant(), slowWaitingHosts, slowWaitingQueue);
        }
        else
        {
          ReleaseTaskOrAddHostToQueueNL(host.ToLowerInvariant(), fastWaitingHosts, fastWaitingQueue);
        }
        return true;
      }
    }

    /// <summary>
    /// Create a waiting task, that will be signaled when notifications for a new host will be available. 
    /// If there are hosts already waiting for processing we return a task with the host name
    /// </summary>
    public Task<List<NotificationData>> TakeAsync(bool slowHostRequested, CancellationToken stoppingToken)
    {
      if (slowHostRequested)
      {
        return GetNextTaskAsync(slowWaitingHosts, slowWaitingQueue, stoppingToken);
      }
      else
      {
        return GetNextTaskAsync(fastWaitingHosts, fastWaitingQueue, stoppingToken);
      }
    }

    public void AddExecutionTime(string host, long executionTime)
    {
      hostExecutionTimes.AddExecutionTime(host, executionTime);
    }
  }
}
