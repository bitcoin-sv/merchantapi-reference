// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

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
using MerchantAPI.APIGateway.Domain.Models.Zmq;
using MerchantAPI.Common.EventBus;
using MerchantAPI.Common.Clock;

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
    EventBusSubscription<RemovedFromMempoolEvent>removedFromMempoolSubscription;
    readonly IBlockParser blockParser;
    readonly IClock clock;

    public InvalidTxHandler(ITxRepository txRepository, ILogger<InvalidTxHandler> logger, IEventBus eventBus, IBlockParser blockParser, IClock clock)
    : base(logger, eventBus)
    {
      this.txRepository = txRepository ?? throw new ArgumentNullException(nameof(txRepository));
      this.blockParser = blockParser ?? throw new ArgumentNullException(nameof(blockParser));
      this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }


    protected override Task ProcessMissedEvents()
    {
      return Task.CompletedTask;
    }


    protected override void UnsubscribeFromEventBus()
    {
      eventBus?.TryUnsubscribe(invalidTxDetectedSubscription);
      invalidTxDetectedSubscription = null;
      
      eventBus?.TryUnsubscribe(removedFromMempoolSubscription);
      removedFromMempoolSubscription = null;
    }


    protected override void SubscribeToEventBus(CancellationToken stoppingToken)
    {
      invalidTxDetectedSubscription = eventBus.Subscribe<InvalidTxDetectedEvent>();
      removedFromMempoolSubscription = eventBus.Subscribe<RemovedFromMempoolEvent>();

      _ = invalidTxDetectedSubscription.ProcessEventsAsync(stoppingToken, logger, InvalidTxDetectedAsync);
      _ = removedFromMempoolSubscription.ProcessEventsAsync(stoppingToken, logger, RemovedFromMempoolEventAsync);

    }

    public async Task RemovedFromMempoolEventAsync(RemovedFromMempoolEvent e)
    {
      if (e.Message.Reason == RemovedFromMempoolMessage.Reasons.CollisionInBlockTx)
      {
        var removedTxId = new uint256(e.Message.TxId).ToBytes();

        var txWithDSCheck = (await txRepository.GetTxsForDSCheckAsync(new[] { removedTxId }, false)).ToArray();
        if (txWithDSCheck.Any())
        {
          // Try to insert the block into DB. If block is already present in DB nothing will be done
          await blockParser.NewBlockDiscoveredAsync(new NewBlockDiscoveredEvent() { CreationDate = clock.UtcNow(),  BlockHash = e.Message.BlockHash });

          foreach (var tx in txWithDSCheck)
          {
            await txRepository.InsertBlockDoubleSpendAsync(
              tx.TxInternalId,
              new uint256(e.Message.BlockHash).ToBytes(),
              new uint256(e.Message.CollidedWith.TxId).ToBytes(),
              HelperTools.HexStringToByteArray(e.Message.CollidedWith.Hex));

            var notificationEvent = new NewNotificationEvent()
            {
              CreationDate = clock.UtcNow(),
              NotificationType = CallbackReason.DoubleSpend,
              TransactionId = tx.TxExternalIdBytes
            };
            eventBus.Publish(notificationEvent);
          }
        }
      }
    }

    public async Task InvalidTxDetectedAsync(InvalidTxDetectedEvent e)
    {
      if (e.Message.RejectionCode == InvalidTxRejectionCodes.TxMempoolConflict ||
          e.Message.RejectionCode == InvalidTxRejectionCodes.TxDoubleSpendDetected)
      {
        if (e.Message.CollidedWith != null && e.Message.CollidedWith.Length > 0)
        {
          var collisionTxList = e.Message.CollidedWith.Select(t => new uint256(t.TxId).ToBytes());
          var txsWithDSCheck = (await txRepository.GetTxsForDSCheckAsync(collisionTxList, true)).ToArray();
          if (txsWithDSCheck.Any())
          {
            var dsTxId = new uint256(e.Message.TxId).ToBytes();
            var dsTxPayload = string.IsNullOrEmpty(e.Message.Hex) ? Array.Empty<byte>() :  HelperTools.HexStringToByteArray(e.Message.Hex);
            foreach (var tx in txsWithDSCheck)
            {
              var inserted = await txRepository.InsertMempoolDoubleSpendAsync(
                                  tx.TxInternalId,
                                  dsTxId,
                                  dsTxPayload);

              if (inserted == 0) return;

              var notificationData = new NotificationData
              {
                TxExternalId = tx.TxExternalIdBytes,
                DoubleSpendTxId = dsTxId,
                CallbackUrl = tx.CallbackUrl,
                CallbackEncryption = tx.CallbackEncryption,
                CallbackToken = tx.CallbackToken,
                TxInternalId = tx.TxInternalId,
                BlockHeight = -1,
                BlockInternalId = -1,
                BlockHash = null
              };

              var notificationEvent = new NewNotificationEvent()
              {
                CreationDate = clock.UtcNow(),
                NotificationType = CallbackReason.DoubleSpendAttempt,
                TransactionId = tx.TxExternalIdBytes,
                NotificationData = notificationData
              };

              eventBus.Publish(notificationEvent);
            }
          }
        }
      }
    }
  }
}
