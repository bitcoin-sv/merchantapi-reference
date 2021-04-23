// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain.Models;
using MerchantAPI.APIGateway.Domain.NotificationsHandler;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Test.Functional
{
  [TestClass]
  public class NotificationHandlerTests : TestBase
  {
    [TestInitialize]
    public void TestInitialize()
    {
      base.Initialize(mockedServices: true);
    }

    [TestCleanup]
    public void TestCleanup()
    {
      base.Cleanup();
    }

    [TestMethod]
    public void HostExecutionTimeClassTest()
    {
      string slowHostName = "slow";
      string fastHostName = "fast";
      var notificationSettings = AppSettings.Notification;
      var hostExecution = new HostExecutionTimes(notificationSettings.NoOfSavedExecutionTimes.Value, notificationSettings.SlowHostThresholdInMs.Value);

      var taskList = new List<Task>();
      for (int i = 0; i < 20; i++)
      {
        hostExecution.AddExecutionTime(slowHostName, new Random(DateTime.UtcNow.Millisecond).Next(notificationSettings.SlowHostThresholdInMs.Value, notificationSettings.SlowHostThresholdInMs.Value + 500));
        hostExecution.AddExecutionTime(fastHostName, new Random(DateTime.UtcNow.Millisecond).Next(0, notificationSettings.SlowHostThresholdInMs.Value));
      }

      Assert.AreEqual(slowHostName, hostExecution.GetHosts(true).Single());
      Assert.AreEqual(fastHostName, hostExecution.GetHosts(false).Single());
    }

    [TestMethod]
    public async Task HostsAsyncProducerConsumerClassTest()
    {
      int maxSlowNotificationSize = 2;
      int maxNotificationQueueSize = 10;
      int batchSize = 20;
      string fastHost = "fastHost";
      string slowHost = "slowHost";
      var pcCollection = new NotificationScheduler(loggerTest, maxSlowNotificationSize, maxNotificationQueueSize, batchSize, 10, 1000);
      pcCollection.AddExecutionTime(slowHost, 2000);
      var notification = new NotificationData()
      {
        TxExternalId = uint256.Zero.ToBytes()
      };

      for (int i = 0; i < maxNotificationQueueSize; i++)
      {
        pcCollection.Add(notification, fastHost);
      }
      Assert.IsFalse(pcCollection.Add(notification, fastHost));

      var notificationList = await pcCollection.TakeAsync(false, CancellationToken.None);
      Assert.AreEqual(maxNotificationQueueSize, notificationList.Count);

      for (int i = 0; i < maxSlowNotificationSize; i++)
      {
        pcCollection.Add(notification, slowHost);
      }
      Assert.IsFalse(pcCollection.Add(notification, slowHost));

      var idleTask = pcCollection.TakeAsync(false, CancellationToken.None);
      Assert.AreEqual(TaskStatus.WaitingForActivation, idleTask.Status);

      pcCollection.Add(notification, fastHost);
      Assert.AreEqual(1, idleTask.Result.Count);

      notificationList = await pcCollection.TakeAsync(true, CancellationToken.None);
      Assert.AreEqual(maxSlowNotificationSize, notificationList.Count);

      idleTask = pcCollection.TakeAsync(true, CancellationToken.None);
      Assert.AreEqual(TaskStatus.WaitingForActivation, idleTask.Status);
    }

    [TestMethod]
    public void CallbackUrlFromattingTest()
    {
      string url1 = "https://test.domain/noPlaceholder";
      string url2 = "https://test.domain/{callbackreason}";
      string url3 = "https://test.domain/{CALLBACKREASON}";
      string url4 = "https://test.domain/{callbackReason}/addedPath";

      var resultUrl = NotificationsHandler.FormatCallbackUrl(url1, "TEST");
      Assert.AreEqual(url1, resultUrl);

      resultUrl = NotificationsHandler.FormatCallbackUrl(url2, "TEST");
      Assert.AreEqual("https://test.domain/TEST", resultUrl);

      resultUrl = NotificationsHandler.FormatCallbackUrl(url3, "TEST");
      Assert.AreEqual("https://test.domain/TEST", resultUrl);

      resultUrl = NotificationsHandler.FormatCallbackUrl(url4, "TEST");
      Assert.AreEqual("https://test.domain/TEST/addedPath", resultUrl);
    }
  }
}
