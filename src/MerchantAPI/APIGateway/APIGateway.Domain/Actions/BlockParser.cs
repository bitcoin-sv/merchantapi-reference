﻿// Copyright(c) 2020 Bitcoin Association.
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
using System.Diagnostics;
using MerchantAPI.APIGateway.Domain.Models.APIStatus;
using MerchantAPI.APIGateway.Domain.Metrics;
using Prometheus;

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
    readonly SemaphoreSlim parseBlockSemaphore = new(1, 1);
    readonly SemaphoreSlim insertTxSemaphore = new(1, 1);
    readonly BlockParserStatus blockParserStatus;
    readonly TimeSpan rpcGetBlockTimeout;
    readonly BlockParserMetrics blockParserMetrics;
    
    EventBusSubscription<NewBlockDiscoveredEvent> newBlockDiscoveredSubscription;
    EventBusSubscription<NewBlockAvailableInDB> newBlockAvailableInDBSubscription;

    long QueueCount => newBlockAvailableInDBSubscription?.QueueCount ?? 0;


    public BlockParser(IRpcMultiClient rpcMultiClient, ITxRepository txRepository, ILogger<BlockParser> logger,
                       IEventBus eventBus, IOptions<AppSettings> options, IClock clock, BlockParserMetrics blockParserMetrics)
    : base(logger, eventBus)
    {
      this.rpcMultiClient = rpcMultiClient ?? throw new ArgumentNullException(nameof(rpcMultiClient));
      this.txRepository = txRepository ?? throw new ArgumentNullException(nameof(txRepository));
      this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
      blockParserStatus = new();
      appSettings = options.Value;
      rpcGetBlockTimeout = TimeSpan.FromMinutes(options.Value.RpcClient.RpcGetBlockTimeoutMinutes.Value);
      this.blockParserMetrics = blockParserMetrics ?? throw new ArgumentNullException(nameof(blockParserMetrics));
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


    private async Task<int> FindAndInsertTxBlockLinkAsync(NBitcoin.Block block, long blockInternalId)
    {
      var txsToCheck = await txRepository.GetTxsNotInCurrentBlockChainAsync(blockInternalId);
      var txIdsFromBlock = new HashSet<uint256>(block.Transactions.Select(x => x.GetHash(Const.NBitcoinMaxArraySize)));

      // Generate a list of transactions that are present in the last block and are also present in our database without a link to existing block
      var txsToLinkToBlock = txsToCheck.Where(x => txIdsFromBlock.Contains(x.TxExternalId)).ToArray();

      return await InsertTxBlockLinkAsync(txsToLinkToBlock, blockInternalId);
    }

    public async Task<int> InsertTxBlockLinkAsync(Tx[] txsToLinkToBlock, long blockInternalId)
    {
      try
      {
        insertTxSemaphore.Wait();
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
      finally
      {
        insertTxSemaphore.Release();
      }
      return txsToLinkToBlock.Length;
    }

    private async Task<int> TransactionsDSCheckAsync(NBitcoin.Block block, long blockInternalId)
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
      }).ToArray();

      // Insert raw data and let the database queries find double spends
      await txRepository.CheckAndInsertBlockDoubleSpendAsync(allTransactionInputs, appSettings.DeltaBlockHeightForDoubleSpendCheck.Value, blockInternalId);

      // Insert DS notifications for unconfirmed ancestors and mark unconfirmed ancestors as processed
      var dsAncestorTxIds = await txRepository.GetDSTxWithoutPayloadAsync(true);
      foreach (var (dsTxId, TxId) in dsAncestorTxIds)
      {
        await txRepository.InsertBlockDoubleSpendForAncestorAsync(TxId);
      }

      // If any new double spend records were generated we need to update them with transaction payload
      // and trigger notification events
      var dsTxIds = await txRepository.GetDSTxWithoutPayloadAsync(false);
      foreach (var (dsTxId, TxId) in dsTxIds)
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
      return dsAncestorTxIds.Count() + dsTxIds.Count();
    }

    public async Task NewBlockDiscoveredAsync(NewBlockDiscoveredEvent e)
    {
      try
      {
        if (appSettings.DontParseBlocks.Value)
        {
          logger.LogInformation($"Block parsing is disabled. Won't store block header information for block '{e.BlockHash}' into database");
          return;
        }

        var blockHash = new uint256(e.BlockHash);

        // If block is already present in DB, there is no need to parse it again
        var blockInDb = await txRepository.GetBlockAsync(blockHash.ToBytes());
        if (blockInDb != null)
        {
          if (blockInDb.OnActiveChain)
          {
            logger.LogDebug($"Block '{e.BlockHash}' already received and stored to DB with onActiveChain.");
            PushBlocksToEventQueue();
          }
          else
          {
            logger.LogDebug($"Block '{e.BlockHash}' already received and stored to DB, updating onActiveChain.");
            await txRepository.SetOnActiveChainBlockAsync(blockInDb.BlockHeight.Value, blockHash.ToBytes());
            await VerifyBlockChainAsync(new uint256(blockInDb.PrevBlockHash).ToString());
          }
          return;
        }

        var blockHeader = await rpcMultiClient.GetBlockHeaderAsync(e.BlockHash);
        var blockCount = (await rpcMultiClient.GetBestBlockchainInfoAsync()).Blocks;

        // If received block that is too far from the best tip, we don't save the block anymore and 
        // stop verifying block chain, but we have to update onActiveChain of old blocks
        if (blockHeader.Height < blockCount - appSettings.MaxBlockChainLengthForFork)
        {
          logger.LogInformation($"Block parser got a new block {e.BlockHash} that is too far from the best tip.");
          // we expect that MaxBlockChainLengthForFork is in sync with CleanUpTxAfterDays 
          // and big enough to catch fork (for now biggest fork was around 100 length)
          // we could also set onActiveChain and trigger NewBlockDiscoveredAsync for every block to the min height in db
          // (or break out before if block already is onActiveChain)
          PushBlocksToEventQueue();
          return;
        }

        logger.LogInformation($"Block parser got a new block {e.BlockHash} inserting into database.");

        //increase counter if block height is greater 
        if ((long)blockParserMetrics.BestBlockHeight.Value < blockHeader.Height)
        {
          blockParserMetrics.BestBlockHeight.IncTo(blockHeader.Height);
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
        var blockId = await txRepository.InsertOrUpdateBlockAsync(dbBlock);

        if (blockId.HasValue)
        {
          dbBlock.BlockInternalId = blockId.Value;
        }
        else
        {
          logger.LogDebug($"Block '{e.BlockHash}' not inserted into DB, because it's already present in DB.");
          // check onActiveChain
          await VerifyBlockChainAsync(new uint256(dbBlock.PrevBlockHash).ToString());
          return;
        }

        newBlockStack.Push(new NewBlockAvailableInDB()
        {
          CreationDate = clock.UtcNow(),
          BlockHash = new uint256(dbBlock.BlockHash).ToString(),
          BlockDBInternalId = dbBlock.BlockInternalId,
          BlockHeight = dbBlock.BlockHeight
        });

        await VerifyBlockChainAsync(blockHeader.Previousblockhash);
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

    private async Task ParseBlockForTransactionsAsync(NewBlockAvailableInDB e)
    {
      try
      {
        await parseBlockSemaphore.WaitAsync();
        try
        {
          blockParserMetrics.BlockParsingQueue.Set(QueueCount);
          if (blockHashesBeingParsed.Any(x => x == e.BlockHash))
          {
            blockParserStatus.IncrementBlocksDuplicated();
            logger.LogDebug($"Block '{e.BlockHash}' is already being parsed...skipped processing.");
            return;
          }
          else if (await txRepository.CheckIfBlockWasParsedAsync(e.BlockDBInternalId))
          {
            blockParserStatus.IncrementBlocksDuplicated();
            logger.LogInformation($"Block '{e.BlockHash}' was already parsed...skipped processing.");
            return;
          }
          else
          {
            blockHashesBeingParsed.Add(e.BlockHash);
          }
        }
        finally
        {
          parseBlockSemaphore.Release();
        }

        logger.LogInformation($"Block parser retrieved a new block {e.BlockHash} from database. Parsing it.");
        using var cts = new CancellationTokenSource(rpcGetBlockTimeout);
        var stopwatch = Stopwatch.StartNew();
        var blockDownloadTime = stopwatch.Elapsed;
        NBitcoin.Block block;
        DateTime blockDownloaded;
        int txsFound, dsFound;
        ulong bytes;
        
        using (blockParserMetrics.BlockParsingDuration.NewTimer())
        {
          using var blockStream = await rpcMultiClient.GetBlockAsStreamAsync(e.BlockHash, cts.Token);
          block = HelperTools.ParseByteStreamToBlock(blockStream);
          bytes = (ulong)blockStream.TotalBytesRead;
          blockDownloaded = DateTime.UtcNow;

          txsFound = await FindAndInsertTxBlockLinkAsync(block, e.BlockDBInternalId);
          dsFound = await TransactionsDSCheckAsync(block, e.BlockDBInternalId);
        }

        stopwatch.Stop();
        var blockParseTime = stopwatch.Elapsed;

        logger.LogInformation($"Block {e.BlockHash} successfully parsed, needed { blockParseTime.TotalMilliseconds } ms.");

        blockParserStatus.IncrementBlocksProcessed(e.BlockHash, e.BlockHeight, txsFound, dsFound, bytes,
          block.Transactions.Count, e.CreationDate, blockParseTime, blockDownloadTime);
        blockParserMetrics.BlockParsingQueue.Set(QueueCount);
        blockParserMetrics.BlockParsed.Inc();
      }
      catch (Exception ex)
      {
        blockParserStatus.IncrementNumOfErrors();
        if (ex is BadRequestException || ex is RpcException)
        {
          logger.LogError(ex.Message);
        }
        else
        {
          throw;
        }
      }
      finally
      {
        await parseBlockSemaphore.WaitAsync();
        try
        {
          blockHashesBeingParsed.Remove(e.BlockHash);
          blockParserMetrics.BlockParsingQueue.Set(QueueCount);
        }
        finally
        {
          parseBlockSemaphore.Release();
        }
      }
    }

    public async Task InitializeDBAsync()
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

        await txRepository.InsertOrUpdateBlockAsync(dbBlock);
      }
    }

    // On each inserted block we check if we have previous block hash
    // If previous block hash doesn't exist it means we either have few missing blocks or we got
    // a block from a fork and we need to fill the gap with missing blocks
    private async Task VerifyBlockChainAsync(string previousBlockHash)
    {
      if (string.IsNullOrEmpty(previousBlockHash) || uint256.Zero.ToString() == previousBlockHash)
      {
        // We reached Genesis block
        PushBlocksToEventQueue();
        return;
      }

      var block = await txRepository.GetBlockAsync(new uint256(previousBlockHash).ToBytes());
      if (block == null || !block.OnActiveChain)
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

    public BlockParserStatus GetBlockParserStatus()
    {
      blockParserStatus.SetBlocksQueued(QueueCount);
      return blockParserStatus;
    }
  }
}
