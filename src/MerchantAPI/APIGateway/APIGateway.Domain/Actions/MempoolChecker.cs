// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain.Models;
using MerchantAPI.APIGateway.Domain.Repositories;
using MerchantAPI.Common.Clock;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Prometheus;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MerchantAPI.APIGateway.Domain.Metrics;

namespace MerchantAPI.APIGateway.Domain.Actions
{
  public class MempoolChecker : BackgroundService, IMempoolChecker
  {
    readonly ILogger<MempoolChecker> logger;
    readonly IRpcMultiClient rpcMultiClient;
    readonly IBlockChainInfo blockChainInfo;
    readonly IMapi mapi;
    readonly IBlockParser blockParser;
    readonly ITxRepository txRepository;
    readonly IClock clock;
    readonly AppSettings appSettings;
    readonly MempoolCheckerMetrics mempoolCheckerMetrics;

    bool success;
    static Dictionary<long, int> txsRetries = new();
    readonly object lockObj = new();
    public bool ResubmitInProcess { get; private set; }

    public bool ExecuteCheckMempoolAndResubmitTxs => !appSettings.DontParseBlocks.Value && !appSettings.MempoolChecker.Disabled.Value;

    public MempoolChecker(
      ILogger<MempoolChecker> logger,
      IRpcMultiClient rpcMultiClient,
      IBlockChainInfo blockChainInfo,
      IMapi mapi,
      IBlockParser blockParser,
      ITxRepository txRepository,
      IClock clock,
      IOptions<AppSettings> options,
      MempoolCheckerMetrics mempoolCheckerMetrics)
    {
      this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
      this.rpcMultiClient = rpcMultiClient ?? throw new ArgumentNullException(nameof(rpcMultiClient));
      this.blockChainInfo = blockChainInfo ?? throw new ArgumentNullException(nameof(blockChainInfo));
      this.mapi = mapi ?? throw new ArgumentNullException(nameof(mapi));
      this.blockParser = blockParser ?? throw new ArgumentNullException(nameof(blockParser));
      this.txRepository = txRepository ?? throw new ArgumentNullException(nameof(txRepository));
      this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
      appSettings = options.Value;
      this.mempoolCheckerMetrics = mempoolCheckerMetrics ?? throw new ArgumentNullException(nameof(mempoolCheckerMetrics));
      success = false;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
      logger.LogInformation("MempoolChecker background service is starting.");
      return base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
      logger.LogInformation("MempoolChecker background service is stopping.");
      return base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
      if (!ExecuteCheckMempoolAndResubmitTxs)
      {
        logger.LogInformation($"`{ nameof(ExecuteCheckMempoolAndResubmitTxs) }` is false. MempoolChecker will not be started up.");
        return;
      }

      await WaitForFirstBlockAsync(stoppingToken);

      while (!stoppingToken.IsCancellationRequested)
      {
        try
        {
          success = await CheckMempoolAndResubmitTxsAsync(appSettings.MempoolChecker.BlockParserQueuedMax.Value);
        }
        catch (Exception ex)
        {
          logger.LogWarning("CheckMempoolAndResubmitTxs failed: " + ex.Message);
          success = false;
          mempoolCheckerMetrics.ExceptionsOnResubmit.Inc();
        }

        if (success)
        {
          await Task.Delay(new TimeSpan(0, 0, appSettings.MempoolChecker.IntervalSec.Value), stoppingToken);
          mempoolCheckerMetrics.SuccessfulResubmits.Inc();
        }
        else
        {
          await Task.Delay(new TimeSpan(0, 0, appSettings.MempoolChecker.UnsuccessfulIntervalSec.Value), stoppingToken);
          mempoolCheckerMetrics.UnsuccessfulResubmits.Inc();
        }
      }
    }

    private async Task WaitForFirstBlockAsync(CancellationToken stoppingToken)
    {
      bool dbIsEmpty;
      bool firstBlockParsed = false;
      do
      {
        dbIsEmpty = await txRepository.GetBestBlockAsync() == null;
        if (!dbIsEmpty)
        {
          firstBlockParsed = blockParser.GetBlockParserStatus().BlocksParsed > 0;
        }
        await Task.Delay(new TimeSpan(0, 0, appSettings.MempoolChecker.UnsuccessfulIntervalSec.Value), stoppingToken);
      } while (dbIsEmpty && !firstBlockParsed);
    }

    public async Task<bool> CheckMempoolAndResubmitTxsAsync(int blockParserQueuedMax)
    {
      // We cannot resubmit only when bestBlockHash is different from last run and blockParser is idle.
      // We can gain/lose mempool txs when block (new fork) is generated, 
      // when maxmempool limit is hit,
      // it can also happen that node suddenly loses all mempool txs
      // - in this last scenario resubmit can take a long time and we should not wait too long...
      try
      {
        var blocks2Parse = await txRepository.GetUnparsedBlocksAsync();
        if (blocks2Parse.Length > blockParserQueuedMax)
        {
          // if we resubmit tx that is actually on active chain (but not yet fixed in our db) it is not a problem, 
          // but we don't want to have too much redundant resubmits
          logger.LogInformation($"blocks2Parse.Length:{ blocks2Parse.Length } > blockParserQueuedMax:{ blockParserQueuedMax }.");
          return false;
        }

        lock (lockObj)
        {
          if (ResubmitInProcess)
          {
            logger.LogInformation($"Resubmit already in process.");
            return false;
          }
          ResubmitInProcess = true;
        }
        var stopwatch = Stopwatch.StartNew();

        var info = await blockChainInfo.GetInfoAsync();
        var blockparserStatus = blockParser.GetBlockParserStatus();
        bool isBlockParserIdle = true;
        if (blockparserStatus.BlocksQueued > 0)
        {
          logger.LogDebug("MempoolChecker: blockparser is processing blocks.");
          isBlockParserIdle = false;
        }

        var resubmitSuccess = await ResubmitTransactionsToNodes();

        lock (lockObj)
        {
          ResubmitInProcess = false;
        }
        stopwatch.Stop();
        logger.LogInformation($"MempoolChecker: resubmit finished with '{ nameof(resubmitSuccess) }'={ resubmitSuccess }, '{ nameof(isBlockParserIdle) }'={ isBlockParserIdle }, took { stopwatch.ElapsedMilliseconds } ms.");

        return resubmitSuccess && isBlockParserIdle;
      }
      catch (Exception)
      {
        lock (lockObj)
        {
          ResubmitInProcess = false;
        }
        throw;
      }
    }

    private async Task<bool> ResubmitTransactionsToNodes()
    {
      var rpcClients = rpcMultiClient.GetRpcClients();
      var txsWithMissingInputsSet = new HashSet<long>();
      bool finalSuccess = true;
      int mempoolCount = 0;
      foreach (var rpcClient in rpcClients)
      {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(appSettings.RpcClient.RpcGetRawMempoolTimeoutMinutes.Value));
        var mempoolCalledAt = clock.UtcNow();
        string[] mempoolTxs = Array.Empty<string>(); ;
        try
        {
          using (mempoolCheckerMetrics.GetRawMempoolDuration.NewTimer())
          {
            mempoolTxs = await rpcClient.GetRawMempool(cts.Token);
          }
        }
        catch
        {
          throw;
        }
        finally
        {
          if (mempoolCount == 0)
          {
            // reset values from previous mempoolChecker run here
            mempoolCheckerMetrics.MaxTxInMempool.Set(mempoolTxs.Length);
            mempoolCheckerMetrics.MinTxInMempool.Set(mempoolTxs.Length);
          }
        }
        mempoolCount = mempoolTxs.Length;
        logger.LogInformation($"{rpcClient} has {mempoolCount} txs in mempool.");
        if (mempoolCheckerMetrics.MaxTxInMempool.Value < mempoolCount)
        {
          mempoolCheckerMetrics.MaxTxInMempool.Set(mempoolCount);
        }
        if (mempoolCheckerMetrics.MinTxInMempool.Value > mempoolCount)
        {
          mempoolCheckerMetrics.MinTxInMempool.Set(mempoolCount);
        }

        var (resubmitSuccess, txsWithMissingInputs) = await mapi.ResubmitMissingTransactionsAsync(mempoolTxs, mempoolCalledAt);
        if (txsWithMissingInputs != null)
        {
          txsWithMissingInputsSet.UnionWith(txsWithMissingInputs);
        }
        finalSuccess &= resubmitSuccess;
      }

      logger.LogDebug($"TxsWithMissingInputs count: {txsWithMissingInputsSet.Count} .");
      await ArrangeTxsWithMissingInputsAsync(txsWithMissingInputsSet.ToList());
      return finalSuccess;
    }

    private async Task ArrangeTxsWithMissingInputsAsync(List<long> txsWithMissingInputs)
    {
      Dictionary<long, int> txsRetriesIncremented = new();
      foreach (var tx in txsWithMissingInputs)
      {
        var retries = txsRetries.GetValueOrDefault(tx);
        txsRetriesIncremented[tx] = retries + 1;
      }
      var txsWithMaxMissingInputs = txsRetriesIncremented.Where(x => x.Value >= appSettings.MempoolChecker.MissingInputsRetries).ToList();
      logger.LogDebug($"TxsWithMaxMissingInputs count: { txsWithMaxMissingInputs.Count } .");
      mempoolCheckerMetrics.TxMissingInputsMax.Inc(txsWithMaxMissingInputs.Count);
      await txRepository.UpdateTxsOnResubmitAsync(Faults.DbFaultComponent.MempoolCheckerUpdateMissingInputs, txsWithMaxMissingInputs.Select(x => new Tx
      {
        TxInternalId = x.Key,
        SubmittedAt = clock.UtcNow(),
        TxStatus = TxStatus.MissingInputsMaxRetriesReached,
        UpdateTx = Tx.UpdateTxMode.TxStatusAndResubmittedAt
      }).ToList());
      txsWithMaxMissingInputs.ForEach(x => txsRetriesIncremented.Remove(x.Key));
      txsRetries = txsRetriesIncremented;
    } 
  }
}
