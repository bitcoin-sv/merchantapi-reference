// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.APIGateway.Domain.Models;
using MerchantAPI.APIGateway.Domain.Repositories;
using MerchantAPI.Common.Json;
using Microsoft.Extensions.Logging;
using NBitcoin;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MerchantAPI.APIGateway.Domain.Models.Events;
using MerchantAPI.Common.EventBus;

namespace MerchantAPI.APIGateway.Domain.Actions
{

  public static class InvalidTxRejectionCodes
  {
    public const int TxMempoolConflict = 258;
    public const int TxDoubleSpendDetected = 18;
  }

  public class InvalidTxHandler : BackgroundServiceWithSubscriptions<InvalidTxHandler>
  {

    readonly ITxRepository txRepository;

    EventBusSubscription<InvalidTxDetectedEvent> invalidTxDetectedSubscription;

    public InvalidTxHandler(ITxRepository txRepository, ILogger<InvalidTxHandler> logger, IEventBus eventBus)
    : base(logger, eventBus)
    {
      this.txRepository = txRepository ?? throw new ArgumentNullException(nameof(txRepository));
    }


    protected override Task ProcessMissedEvents()
    {
      return Task.CompletedTask;
    }


    protected override void UnsubscribeFromEventBus()
    {
      eventBus?.TryUnsubscribe(invalidTxDetectedSubscription);
      invalidTxDetectedSubscription = null;
    }


    protected override void SubscribeToEventBus(CancellationToken stoppingToken)
    {
      invalidTxDetectedSubscription = eventBus.Subscribe<InvalidTxDetectedEvent>();

      _ = invalidTxDetectedSubscription.ProcessEventsAsync(stoppingToken, logger, InvalidTxDetectedAsync);
    }


    public async Task InvalidTxDetectedAsync(InvalidTxDetectedEvent e)
    {
      if (e.Message.RejectionCode == InvalidTxRejectionCodes.TxMempoolConflict ||
          e.Message.RejectionCode == InvalidTxRejectionCodes.TxDoubleSpendDetected)
      {
        if (e.Message.CollidedWith != null && e.Message.CollidedWith.Length > 0)
        {
          var collisionTxList = e.Message.CollidedWith.Select(t => new uint256(t.TxId).ToBytes());
          var txsWithDSCheck = (await txRepository.GetTxsForDSCheckAsync(collisionTxList)).ToArray();
          if (txsWithDSCheck.Any())
          {
            var dsTxId = new uint256(e.Message.TxId).ToBytes();
            var dsTxPayload = string.IsNullOrEmpty(e.Message.Hex) ? new byte[0] :  HelperTools.HexStringToByteArray(e.Message.Hex);
            foreach (var tx in txsWithDSCheck)
            {
              await txRepository.InsertMempoolDoubleSpendAsync(
                tx.TxInternalId,
                dsTxId,
                dsTxPayload);
              var notificationEvent = new NewNotificationEvent
                                      {
                                        NotificationType = CallbackReason.DoubleSpendAttempt,
                                        TransactionId = tx.TxExternalId
                                      };
              var notificationData = new NotificationData
                                    {
                                      TxExternalId = tx.TxExternalId,
                                      DoubleSpendTxId = dsTxId,
                                      Payload = dsTxPayload,
                                      CallbackUrl = tx.CallbackUrl,
                                      CallbackEncryption = tx.CallbackEncryption,
                                      CallbackToken = tx.CallbackToken,
                                      TxInternalId = tx.TxInternalId
                                    };
              if (NotificationAction.AddNotificationData(notificationEvent, notificationData))
              {
                eventBus.Publish(notificationEvent);
              }
            }
          }
        }
      }
    }
  }
}
