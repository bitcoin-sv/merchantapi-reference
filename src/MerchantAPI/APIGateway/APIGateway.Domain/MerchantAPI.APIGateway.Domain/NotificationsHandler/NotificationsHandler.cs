// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.APIGateway.Domain.Actions;
using MerchantAPI.APIGateway.Domain.ExternalServices;
using MerchantAPI.APIGateway.Domain.Models;
using MerchantAPI.APIGateway.Domain.Models.Events;
using MerchantAPI.APIGateway.Domain.Repositories;
using MerchantAPI.APIGateway.Domain.ViewModels;
using MerchantAPI.Common.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Domain.NotificationsHandler
{
  public class NotificationsHandler : BackgroundService, INotificationsHandler
  {
    readonly ILogger<NotificationsHandler> logger;
    readonly INotificationServiceHttpClientFactory httpClientFactory;
    readonly ITxRepository txRepository;
    readonly IRpcMultiClient rpcMultiClient;
    readonly IMinerId minerId;
    readonly Notification notificationSettings;

    readonly NotificationScheduler notificationScheduler;

    public NotificationsHandler(ILogger<NotificationsHandler> logger, INotificationServiceHttpClientFactory httpClientFactory, IOptions<AppSettings> options, 
                                ITxRepository txRepository, IRpcMultiClient rpcMultiClient, IMinerId minerId)
    {
      this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
      this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
      this.txRepository = txRepository ?? throw new ArgumentNullException(nameof(txRepository));
      this.rpcMultiClient = rpcMultiClient ?? throw new ArgumentNullException(nameof(rpcMultiClient));
      this.minerId = minerId ?? throw new ArgumentNullException(nameof(minerId));
      notificationSettings = options.Value.Notification;
      var maxNumberOfSlowNotifications = notificationSettings.InstantNotificationsQueueSize * notificationSettings.InstantNotificationsSlowTaskPercentage / 100;
      notificationScheduler = new NotificationScheduler(logger, maxNumberOfSlowNotifications, notificationSettings.InstantNotificationsQueueSize,
                                                        notificationSettings.MaxNotificationsInBatch, notificationSettings.NoOfSavedExecutionTimes,
                                                        notificationSettings.SlowHostThresholdInMs);
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
      logger.LogInformation("Starting notification handler background service");

      return base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
      logger.LogInformation("Stopping notification handler background service");

      return base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
      await Task.WhenAll(ExecuteTasksAsync(true, stoppingToken), ExecuteTasksAsync(false, stoppingToken)); ;
    }

    public HttpClient GetClient(string callbackUrl)
    {
      var uri = new Uri(callbackUrl);
      return httpClientFactory.CreateClient(uri.Host.ToLowerInvariant());
    }

    public async Task MarkUncompleteNotificationsAsFailedAsync()
    {
      await txRepository.MarkUncompleteNotificationsAsFailedAsync();
    }
    
    /// <summary>
    /// Prepare worker tasks for slow and fast http clients and execute them
    /// </summary>
    private async Task ExecuteTasksAsync(bool slowClient, CancellationToken stoppingToken)
    {
      var numOfSlowTasks = (int)Math.Ceiling(notificationSettings.InstantNotificationsTasks * ((double)notificationSettings.InstantNotificationsSlowTaskPercentage / 100));
      int numOfTasks = slowClient ? numOfSlowTasks : notificationSettings.InstantNotificationsTasks - numOfSlowTasks;
      logger.LogInformation($"Starting up '{numOfTasks}' of {(slowClient ? "slow" : "fast")} tasks");
      List<Task> executingTasks = new List<Task>();

      do
      {
        do
        {
          var task = DoWorkAsync(slowClient, stoppingToken);
          executingTasks.Add(task);
        }
        while (executingTasks.Count < numOfTasks);
        var finishedTask = await Task.WhenAny(executingTasks.ToArray());
        executingTasks.Remove(finishedTask);
        stoppingToken.ThrowIfCancellationRequested();
      }
      while (true);
    }

    /// <summary>
    /// Worker task logic. Wait for new notifications and start processing and sending them out when they arrive
    /// </summary>
    private async Task DoWorkAsync(bool slowHostRequested, CancellationToken stoppingToken)
    {
      var notificationsToSend = await notificationScheduler.TakeAsync(slowHostRequested, stoppingToken);
      int requestTimeout = slowHostRequested ? notificationSettings.SlowHostResponseTimeoutMS : notificationSettings.FastHostResponseTimeoutMS;
      if (notificationsToSend?.Count > 0)
      {
        // First callbackUrl is taken because all urls in the list have the same host name
        using var client = GetClient(notificationsToSend.First().CallbackUrl);
        foreach (var notification in notificationsToSend)
        {
          try
          {
            await ProcessNotificationAsync(client, notification, requestTimeout, stoppingToken);
          }
          catch (Exception ex)
          {
            logger.LogCritical(ex, "Error while trying to process notification events");
          }
          stoppingToken.ThrowIfCancellationRequested();
        }
      }
    }

    /// <summary>
    /// Execute the call to callback url with provided payload
    /// </summary>
    /// <returns>true if the call succeeded, false if the call failed</returns>
    private async Task<string> InitiateCallbackAsync(HttpClient client, string callbackUrl, string callbackToken, 
                                                     string callBackEncryption, string payload, int requestTimeout, 
                                                     CancellationToken stoppingToken)
    {
      var hostURI = new Uri(callbackUrl);
      var stopwatch = Stopwatch.StartNew();
      string errMessage = null;
      try
      {
        var restClient = new RestClient(callbackUrl, callbackToken, client);
        var notificationTimeout = new TimeSpan(0, 0, 0, 0, requestTimeout);
        if (string.IsNullOrEmpty(callBackEncryption))
        {
          _ = await restClient.PostJsonAsync("", payload, requestTimeout: notificationTimeout, token: stoppingToken);
        }
        else
        {
          _ = await restClient.PostOctetStream("", MapiEncryption.Encrypt(payload, callBackEncryption), requestTimeout: notificationTimeout, token: stoppingToken);
        }

        logger.LogDebug($"Successfully sent notification to '{callbackUrl}', with execution time of '{stopwatch.ElapsedMilliseconds}'ms");
      }
      catch (Exception ex)
      {
        errMessage = ex.GetBaseException().Message;
        logger.LogError($"Callback failed for host {hostURI.Host}. Error: {ex.GetBaseException().Message}");
      }
      finally
      {
        stopwatch.Stop();
        notificationScheduler.AddExecutionTime(hostURI.Host, stopwatch.ElapsedMilliseconds);
      }
      return errMessage;
    }

    string lastMinerId;
    private async Task<string> SignIfRequiredAsync<T>(T response)
    {
      string payload = HelperTools.JSONSerialize(response, false);

      if (minerId == null)
      {
        // Do not sign if we do not have miner id
        return payload;
      }

      lastMinerId ??= await minerId.GetCurrentMinerIdAsync();

      async Task<JsonEnvelope> TryToSign()
      {
        Func<string, Task<(string signature, string publicKey)>> signWithMinerId = async sigHashHex =>
        {
          var signature = await minerId.SignWithMinerIdAsync(lastMinerId, sigHashHex);

          return (signature, lastMinerId);
        };

        var envelope = await JsonEnvelopeSignature.CreateJSonSignatureAsync(payload, signWithMinerId);

        // Verify a signature - some implementation might have incorrect race conditions when rotating the keys
        if (!JsonEnvelopeSignature.VerifySignature(envelope))
        {
          return null;
        }

        return envelope;
      }

      var jsonEnvelope = await TryToSign();

      if (jsonEnvelope == null)
      {
        throw new Exception("Error while validating signature. Possible reason: incorrect configuration or key rotation");
      }
      return HelperTools.JSONSerialize(new SignedPayloadViewModel(jsonEnvelope), true);
    }

    /// <summary>
    /// Prepare payload for the callback url and execute the call
    /// </summary>
    private async Task<string> SendNotificationAsync(HttpClient client, NotificationData notificationData, int requestTimeout, CancellationToken stoppingToken)
    {
      var cbNotification = CallbackNotificationViewModelBase.CreateFromNotificationData(notificationData);
      cbNotification.MinerId = lastMinerId ?? await minerId.GetCurrentMinerIdAsync();

      string signedPayload;
      try
      {
        signedPayload = await SignIfRequiredAsync(cbNotification);
      }
      catch(Exception ex)
      {
        logger.LogError($"Unable to sign the notification payload. Error: {ex.GetBaseException().Message}");
        return ex.GetBaseException().Message;
      }

      return await InitiateCallbackAsync(client, notificationData.CallbackUrl, notificationData.CallbackToken, 
                                         notificationData.CallbackEncryption, signedPayload, requestTimeout, stoppingToken);
    }


    /// <summary>
    /// Method for instant processing of notification events. Events that will fail here will be picked up by a background job for retrial.
    /// </summary>
    public async Task<bool> ProcessNotificationAsync(HttpClient client, NotificationData notification, int requestTimeout, CancellationToken stoppingToken)
    {
      logger.LogDebug($"Processing notification '{notification.NotificationType}' for transaction '{new uint256(notification.TxExternalId)}' for host '{client.BaseAddress}'");
      
      try
      {
        switch (notification.NotificationType)
        {
          case CallbackReason.DoubleSpend:
            notification.Payload = await txRepository.GetDoublespendTxPayloadAsync(notification.NotificationType, notification.TxInternalId);
            break;

          case CallbackReason.DoubleSpendAttempt:
            notification.Payload = await txRepository.GetDoublespendTxPayloadAsync(notification.NotificationType, notification.TxInternalId);
            break;

          case CallbackReason.MerkleProof:
            notification.MerkleProof = await rpcMultiClient.GetMerkleProofAsync(new uint256(notification.TxExternalId).ToString(), new uint256(notification.BlockHash).ToString());
            break;
          default:
            throw new InvalidOperationException($"Invalid notification type {notification.NotificationType}");
        }
      }
      catch (Exception ex)
      {
        logger.LogError($"Unable to prepare notification for {new uint256(notification.TxExternalId)}. Error: {ex.GetBaseException().Message}");
        await txRepository.SetNotificationErrorAsync(notification.TxExternalId, notification.NotificationType, ex.GetBaseException().Message, ++notification.ErrorCount);
        return false;
      }

      var errMessage = await SendNotificationAsync(client, notification, requestTimeout, stoppingToken);
      if (errMessage == null)
      {
        await txRepository.SetNotificationSendDateAsync(notification.NotificationType, notification.TxInternalId, notification.BlockInternalId, notification.DoubleSpendTxId, DateTime.UtcNow);
        return true;
      }
      else
      { 
        await txRepository.SetNotificationErrorAsync(notification.TxExternalId, notification.NotificationType, errMessage, ++notification.ErrorCount);
        return false;
      }
    }

    /// <summary>
    /// Store the notification event into the queue to be picked up by instant notification processor or a background job
    /// if the queue is already full (determined by `MaxInstantNotificationsQueueSize` setting)
    /// </summary>
    public async Task EnqueueNotificationAsync(NewNotificationEvent notificationEvent)
    {
      try
      {
        // Prepare notification data from DB before writing it to queue
        NotificationData notificationData = null;
        switch (notificationEvent.NotificationType)
        {
          case CallbackReason.DoubleSpend:
            notificationData = await txRepository.GetTxToSendBlockDSNotificationAsync(notificationEvent.TransactionId);
            break;
          case CallbackReason.DoubleSpendAttempt:
            notificationData = notificationEvent.NotificationData;
            break;
          case CallbackReason.MerkleProof:
            notificationData = await txRepository.GetTxToSendMerkleProofNotificationAsync(notificationEvent.TransactionId);
            break;
          default:
            throw new InvalidOperationException($"Invalid notification type {notificationEvent.NotificationType}");
        }
        notificationData.NotificationType = notificationEvent.NotificationType;
        notificationData.CreatedAt = DateTime.UtcNow;
        Uri uri = new Uri(notificationData.CallbackUrl);
        var host = uri.Host.ToLower();

        if (!notificationScheduler.Add(notificationData, host))
        {
          // Error count is set to 0, because there was no attempt to send it out yet, we just mark it for non-instant notification
          await txRepository.SetNotificationErrorAsync(notificationEvent.TransactionId, notificationEvent.NotificationType, "Queue is full or too many notifications for slow hosts.", 0);
          return;
        }
      }
      catch (Exception ex)
      {
        logger.LogError($"Error while trying to enqueue notification '{notificationEvent.NotificationType}' for {new uint256(notificationEvent.TransactionId)}. Error: {ex.GetBaseException().Message}");

        // Error count is set to 0, because there was no attempt to send it out yet, we just mark it for non-instant notification
        await txRepository.SetNotificationErrorAsync(notificationEvent.TransactionId, notificationEvent.NotificationType, ex.GetBaseException().Message, 0);
      }
    }
  }
}
