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
using MerchantAPI.Common.EventBus;
using Block = MerchantAPI.APIGateway.Domain.Models.Block;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using MerchantAPI.Common.Clock;
using MerchantAPI.Common.BitcoinRpc;
using MerchantAPI.Common.Exceptions;

namespace MerchantAPI.APIGateway.Domain.Actions
{

  public class BlockParser : BackgroundServiceWithSubscriptions<BlockParser>, IBlockParser
  {
    // Use stack for storing new blocks before triggering event for parsing blocks, to ensure
    // that blocks will be parsed in same order as they were added to the blockchain
    readonly Stack<NewBlockAvailableInDB> newBlockStack = new();
    readonly AppSettings appSettings;
    readonly ITxRepository txRepository;
    readonly IRpcMultiClient rpcMultiClient;
    readonly IClock clock;
    readonly List<string> blockHashesBeingParsed = new();
    readonly object lockingObject = new();

    EventBusSubscription<NewBlockDiscoveredEvent> newBlockDiscoveredSubscription;
    EventBusSubscription<NewBlockAvailableInDB> newBlockAvailableInDBSubscription;


    public BlockParser(IRpcMultiClient rpcMultiClient, ITxRepository txRepository, ILogger<BlockParser> logger, 
                       IEventBus eventBus, IOptions<AppSettings> options, IClock clock)
    : base(logger, eventBus)
    {
      this.rpcMultiClient = rpcMultiClient ?? throw new ArgumentNullException(nameof(rpcMultiClient));
      this.txRepository = txRepository ?? throw new ArgumentNullException(nameof(txRepository));
      this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
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
      var txsToCheck = await txRepository.GetTxsNotInCurrentBlockChainAsync(blockInternalId);
      var txIdsFromBlock = new HashSet<uint256>(block.Transactions.Select(x => x.GetHash(Const.NBitcoinMaxArraySize)));

      // Generate a list of transactions that are present in the last block and are also present in our database without a link to existing block
      var txsToLinkToBlock = txsToCheck.Where(x => txIdsFromBlock.Contains(x.TxExternalId)).ToArray();

      await txRepository.InsertTxBlockAsync(txsToLinkToBlock.Select(x => x.TxInternalId).ToList(), blockInternalId);
      foreach (var transactionForMerkleProofCheck in txsToLinkToBlock.Where(x => x.MerkleProof).ToArray())
      {
        var notificationEvent = new NewNotificationEvent()
                                {
                                  CreationDate = clock.UtcNow(),
                                  NotificationType = CallbackReason.MerkleProof,
                                  TransactionId = transactionForMerkleProofCheck.TxExternalIdBytes
                                };
        eventBus.Publish(notificationEvent);
      }
      await txRepository.SetBlockParsedForMerkleDateAsync(blockInternalId);
    }

    private async Task TransactionsDSCheckAsync(NBitcoin.Block block, long blockInternalId)
    {
      // Inputs are flattened along with transactionId so they can be checked for double spends.
      var allTransactionInputs = block.Transactions.SelectMany(x => x.Inputs.AsIndexedInputs(), (tx, txIn) => new 
                                                                    { 
                                                                      TxId = tx.GetHash(Const.NBitcoinMaxArraySize).ToBytes(),
                                                                      TxInput = txIn
                                                                    }).Select(x => new TxWithInput
                                                                    {
                                                                      TxExternalIdBytes = x.TxId,
                                                                      PrevTxId = x.TxInput.PrevOut.Hash.ToBytes(),
                                                                      Prev_N = x.TxInput.PrevOut.N
                                                                    });

      // Insert raw data and let the database queries find double spends
      await txRepository.CheckAndInsertBlockDoubleSpendAsync(allTransactionInputs, appSettings.DeltaBlockHeightForDoubleSpendCheck, blockInternalId);

      // Insert DS notifications for unconfirmed ancestors and mark unconfirmed ancestors as processed
      var dsAncestorTxIds = await txRepository.GetDSTxWithoutPayloadAsync(true);
      foreach (var (dsTxId, TxId) in dsAncestorTxIds)
      {
        await txRepository.InsertBlockDoubleSpendForAncestorAsync(TxId);
      }

      // If any new double spend records were generated we need to update them with transaction payload
      // and trigger notification events
      var dsTxIds = await txRepository.GetDSTxWithoutPayloadAsync(false);
      foreach(var (dsTxId, TxId) in dsTxIds)
      {
        var payload = block.Transactions.Single(x => x.GetHash(Const.NBitcoinMaxArraySize) == new uint256(dsTxId)).ToBytes();
        await txRepository.UpdateDsTxPayloadAsync(dsTxId, payload);
        var notificationEvent = new NewNotificationEvent()
        {
                                  CreationDate = clock.UtcNow(),
                                  NotificationType = CallbackReason.DoubleSpend,
                                  TransactionId = TxId
        };
        eventBus.Publish(notificationEvent);
      }
      await txRepository.SetBlockParsedForDoubleSpendDateAsync(blockInternalId);
    }


    public async Task NewBlockDiscoveredAsync(NewBlockDiscoveredEvent e)
    {
      try
      {
        if (appSettings.DontParseBlocks)
        {
          logger.LogInformation($"Block parsing is disabled. Won't store block header information for block '{e.BlockHash}' into database");
          return;
        }

        var blockHash = new uint256(e.BlockHash);

        // If block is already present in DB, there is no need to parse it again
        var blockInDb = await txRepository.GetBlockAsync(blockHash.ToBytes());
        if (blockInDb != null)
        {
          logger.LogDebug($"Block '{e.BlockHash}' already received and stored to DB.");
          return;
        }

        logger.LogInformation($"Block parser got a new block {e.BlockHash} inserting into database.");
        var blockHeader = await rpcMultiClient.GetBlockHeaderAsync(e.BlockHash);
        var blockCount = (await rpcMultiClient.GetBestBlockchainInfoAsync()).Blocks;

        // If received block that is too far from the best tip, we don't save the block anymore and 
        // stop verifying block chain
        if (blockHeader.Height < blockCount - appSettings.MaxBlockChainLengthForFork)
        {
          PushBlocksToEventQueue();
          return;
        }

        var dbBlock = new Block
        {
          BlockHash = blockHash.ToBytes(),
          BlockHeight = blockHeader.Height,
          BlockTime = HelperTools.GetEpochTime(blockHeader.Time),
          OnActiveChain = true,
          PrevBlockHash = blockHeader.Previousblockhash == null ? uint256.Zero.ToBytes() : new uint256(blockHeader.Previousblockhash).ToBytes()
        };

        // Insert block in DB and add the event to block stack for later processing
        var blockId = await txRepository.InsertBlockAsync(dbBlock);

        if (blockId.HasValue)
        {
          dbBlock.BlockInternalId = blockId.Value;
        }
        else
        {
          logger.LogDebug($"Block '{e.BlockHash}' not inserted into DB, because it's already present in DB.");
          return;
        }

        newBlockStack.Push(new NewBlockAvailableInDB()
        {
          CreationDate = clock.UtcNow(),
          BlockHash = new uint256(dbBlock.BlockHash).ToString(),
          BlockDBInternalId = dbBlock.BlockInternalId,
        });
        _ = VerifyBlockChain(blockHeader.Previousblockhash);
      }
      catch(BadRequestException ex)
      {
        logger.LogError(ex.Message);
      }
      catch(RpcException ex)
      {
        logger.LogError(ex.Message);
      }
    }

    private async Task ParseBlockForTransactionsAsync(NewBlockAvailableInDB e)
    {
      try
      {
        lock (lockingObject)
        {
          if (blockHashesBeingParsed.Any(x => x == e.BlockHash))
          {
            logger.LogDebug($"Block '{e.BlockHash}' is already being parsed...skiped processing.");
            return;
          }
          else
          {
            blockHashesBeingParsed.Add(e.BlockHash);
          }
        }

        logger.LogInformation($"Block parser retrieved a new block {e.BlockHash} from database. Parsing it.");
        var blockStream = await rpcMultiClient.GetBlockAsStreamAsync(e.BlockHash);

        var block = HelperTools.ParseByteStreamToBlock(blockStream);

        await InsertTxBlockLinkAsync(block, e.BlockDBInternalId);
        await TransactionsDSCheckAsync(block, e.BlockDBInternalId);

        logger.LogInformation($"Block {e.BlockHash} successfully parsed.");
        lock (lockingObject)
        {
          blockHashesBeingParsed.Remove(e.BlockHash);
        }
      }
      catch (BadRequestException ex)
      {
        logger.LogError(ex.Message);
      }
      catch (RpcException ex)
      {
        logger.LogError(ex.Message);
      }
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
      if (string.IsNullOrEmpty(previousBlockHash) || uint256.Zero.ToString() == previousBlockHash)
      {
        // We reached Genesis block
        PushBlocksToEventQueue();
        return;
      }

      var block = await txRepository.GetBlockAsync(new uint256(previousBlockHash).ToBytes());
      if (block == null)
      {
        await NewBlockDiscoveredAsync(new NewBlockDiscoveredEvent()
        {
          CreationDate = clock.UtcNow(),
          BlockHash = previousBlockHash
        });
      }
      else
      {
        PushBlocksToEventQueue();
      }
    }

    private void PushBlocksToEventQueue()
    {
      if (newBlockStack.Count > 0)
      {
        do
        {
          var newBlockEvent = newBlockStack.Pop();
          eventBus.Publish(newBlockEvent);
        } while (newBlockStack.Any());
      }
    }
  }
}
