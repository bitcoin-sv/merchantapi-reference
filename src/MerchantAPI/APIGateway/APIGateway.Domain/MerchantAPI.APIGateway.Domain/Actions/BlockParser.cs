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
using Block = MerchantAPI.APIGateway.Domain.Models.Block;
using Microsoft.Extensions.Options;

namespace MerchantAPI.APIGateway.Domain.Actions
{

  public class BlockParser : BackgroundServiceWithSubscriptions<BlockParser>, IBlockParser
  {
    readonly AppSettings appSettings;
    readonly ITxRepository txRepository;
    readonly IRpcMultiClient rpcMultiClient;
    readonly INotificationAction notificationAction;

    EventBusSubscription<NewBlockDiscoveredEvent> newBlockDiscoveredSubscription;
    EventBusSubscription<NewBlockAvailableInDB> newBlockAvailableInDBSubscription;


    public BlockParser(IRpcMultiClient rpcMultiClient, ITxRepository txRepository, ILogger<BlockParser> logger, 
                       IEventBus eventBus, IOptions<AppSettings> options, INotificationAction notificationAction)
    : base(logger, eventBus)
    {
      this.rpcMultiClient = rpcMultiClient ?? throw new ArgumentNullException(nameof(rpcMultiClient));
      this.txRepository = txRepository ?? throw new ArgumentNullException(nameof(txRepository));
      this.notificationAction = notificationAction ?? throw new ArgumentNullException(nameof(notificationAction));
      appSettings = options.Value;
    }


    protected override Task ProcessMissedEvents()
    {
      return Task.CompletedTask; 
    }


    protected override void UnsubscribeFromEventBus()
    {
      eventBus?.TryUnsubscribe(newBlockDiscoveredSubscription);
      newBlockDiscoveredSubscription = null;
      eventBus?.TryUnsubscribe(newBlockAvailableInDBSubscription);
      newBlockAvailableInDBSubscription = null;
    }


    protected override void SubscribeToEventBus(CancellationToken stoppingToken)
    {
      newBlockDiscoveredSubscription = eventBus.Subscribe<NewBlockDiscoveredEvent>();
      newBlockAvailableInDBSubscription = eventBus.Subscribe<NewBlockAvailableInDB>();

      _ = newBlockDiscoveredSubscription.ProcessEventsAsync(stoppingToken, logger, NewBlockDiscoveredAsync);
      _ = newBlockAvailableInDBSubscription.ProcessEventsAsync(stoppingToken, logger, ParseBlockForTransactionsAsync);
    }

    
    private async Task InsertTxBlockLinkAsync(NBitcoin.Block block, long blockInternalId)
    {
      var txsToCheck = await txRepository.GetTxsWithoutBlockAsync();

      // Generate a list of transactions that are present in the last block and are also present in our database without a link to existing block
      var transactionsForMerkleProofCheck = txsToCheck.Where(y => block.Transactions.Any(x => new uint256(y.TxExternalId) == x.GetHash()));

      await txRepository.InsertTxBlockAsync(transactionsForMerkleProofCheck.Select(x => x.TxInternalId).ToList(), blockInternalId);
      foreach (var transaction in transactionsForMerkleProofCheck)
      {
        var notificationEvent = new NewNotificationEvent
                                {
                                  NotificationType = CallbackReason.MerkleProof,
                                  TransactionId = transaction.TxExternalId
                                };
        if (NotificationAction.AddNotificationData(notificationEvent, null))
        {
          eventBus.Publish(notificationEvent);
        }
      }
    }

    private async Task TransactionsDSCheckAsync(NBitcoin.Block block, long blockInternalId)
    {
      // Inputs are flattened along with transactionId so they can be checked for double spends.
      var allTransactionInputs = block.Transactions.SelectMany(x => x.Inputs.AsIndexedInputs(), (tx, txIn) => new 
                                                                    { 
                                                                      TxId = tx.GetHash().ToBytes(),
                                                                      TxInput = txIn
                                                                    }).Select(x => new TxWithInput
                                                                    {
                                                                      TxExternalId = x.TxId,
                                                                      PrevTxId = x.TxInput.PrevOut.Hash.ToBytes(),
                                                                      Prev_N = x.TxInput.PrevOut.N
                                                                    });

      // Insert raw data and let the database queries find double spends
      await txRepository.CheckAndInsertBlockDoubleSpendAsync(allTransactionInputs, appSettings.DeltaBlockHeightForDoubleSpendCheck, blockInternalId);

      // If any new double spend records were generated we need to update them with transaction payload
      var dsTxIds = await txRepository.GetDSTxWithoutPayload();
      foreach(var dsTx in dsTxIds)
      {
        var payload = block.Transactions.Single(x => x.GetHash() == new uint256(dsTx)).ToBytes();
        await txRepository.UpdateDsTxPayload(dsTx, payload);
        var notificationEvent = new NewNotificationEvent
                                {
                                  NotificationType = CallbackReason.DoubleSpend,
                                  TransactionId = dsTx
                                };
        if (NotificationAction.AddNotificationData(notificationEvent, null))
        {
          eventBus.Publish(notificationEvent);
        }
      }
      await txRepository.SetBlockParsedForDoubleSpendDateAsync(blockInternalId);
    }


    private async Task NewBlockDiscoveredAsync(NewBlockDiscoveredEvent e)
    {
      logger.LogInformation($"Block parser got a new block {e.BlockHash} inserting into database");
      var blockHash = new uint256(e.BlockHash);
      var blockHeader = await rpcMultiClient.GetBlockHeaderAsync(e.BlockHash);

      var dbBlock = new Block
      {
        BlockHash = blockHash.ToBytes(),
        BlockHeight = blockHeader.Height,
        BlockTime = HelperTools.GetEpochTime(blockHeader.Time),
        OnActiveChain = true,
        PrevBlockHash = blockHeader.Previousblockhash == null ? uint256.Zero.ToBytes() : new uint256(blockHeader.Previousblockhash).ToBytes()
      };

      // Insert block in DB and trigger NewBlockAvailableInDB event
      dbBlock.BlockInternalId = await txRepository.InsertBlockAsync(dbBlock);

      eventBus.Publish(new NewBlockAvailableInDB
      {
        BlockHash = new uint256(dbBlock.BlockHash).ToString(),
        BlockDBInternalId = dbBlock.BlockInternalId,
      });
      await VerifyBlockChain(blockHeader.Previousblockhash);
    }

    private async Task ParseBlockForTransactionsAsync(NewBlockAvailableInDB e)
    {
      logger.LogInformation($"Block parser got a new block {e.BlockHash} from database. Parsing it");
      var blockBytes = await rpcMultiClient.GetBlockAsBytesAsync(e.BlockHash);

      var block = HelperTools.ParseBytesToBlock(blockBytes);

      await InsertTxBlockLinkAsync(block, e.BlockDBInternalId);
      await TransactionsDSCheckAsync(block, e.BlockDBInternalId);
    }

    public async Task InitializeDB()
    {
      var dbIsEmpty = await txRepository.GetBestBlockAsync() == null;

      var bestBlockHash = (await rpcMultiClient.GetBestBlockchainInfoAsync()).BestBlockHash;

      if (dbIsEmpty)
      {
        var blockHeader = await rpcMultiClient.GetBlockHeaderAsync(bestBlockHash);

        var dbBlock = new Block
        {
          BlockHash = new uint256(bestBlockHash).ToBytes(),
          BlockHeight = blockHeader.Height,
          BlockTime = HelperTools.GetEpochTime(blockHeader.Time),
          OnActiveChain = true,
          PrevBlockHash = blockHeader.Previousblockhash == null ? uint256.Zero.ToBytes() : new uint256(blockHeader.Previousblockhash).ToBytes()
        };

        await txRepository.InsertBlockAsync(dbBlock);
      }
    }

    // On each inserted block we check if we have previous block hash
    // If previous block hash doesn't exist it means we either have few missing blocks or we got
    // a block from a fork and we need to fill the gap with missing blocks
    private async Task VerifyBlockChain(string previousBlockHash)
    {
      if (string.IsNullOrEmpty(previousBlockHash))
      {
        // We reached Genesis block
        return;
      }

      var block = await txRepository.GetBlockAsync(new uint256(previousBlockHash).ToBytes());

      if (block == null)
      {
        await NewBlockDiscoveredAsync(new NewBlockDiscoveredEvent
        {
          BlockHash = previousBlockHash
        });
      }
    }
  }
}
