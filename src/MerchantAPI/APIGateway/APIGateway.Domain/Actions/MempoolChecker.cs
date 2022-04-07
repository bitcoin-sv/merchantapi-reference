// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain.Models;
using MerchantAPI.APIGateway.Domain.Repositories;
using MerchantAPI.Common.Clock;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Domain.Actions
{
  public class MempoolChecker : BackgroundService, IMempoolChecker
  {
    readonly ILogger<MempoolChecker> logger;
    readonly IBlockChainInfo blockChainInfo;
    readonly IMapi mapi;
    readonly IBlockParser blockParser;
    readonly ITxRepository txRepository;
    readonly IClock clock;
    readonly AppSettings appSettings;

    string lastRefreshAtBlockHash;
    bool success;
    static Dictionary<long, int> txsRetries = new();

    public bool ExecuteCheckMempoolAndResubmitTxs => !appSettings.DontParseBlocks.Value && appSettings.MempoolCheckerEnabled.Value;

    public MempoolChecker(ILogger<MempoolChecker> logger, IBlockChainInfo blockChainInfo, IMapi mapi, IBlockParser blockParser, ITxRepository txRepository, IClock clock, IOptions<AppSettings> options)
    {
      this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
      this.blockChainInfo = blockChainInfo ?? throw new ArgumentNullException(nameof(blockChainInfo));
      this.mapi = mapi ?? throw new ArgumentNullException(nameof(mapi));
      this.blockParser = blockParser ?? throw new ArgumentNullException(nameof(blockParser));
      this.txRepository = txRepository ?? throw new ArgumentNullException(nameof(txRepository));
      this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
      appSettings = options.Value;
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

      while (!stoppingToken.IsCancellationRequested)
      {
        try
        {
          success = await CheckMempoolAndResubmitTxs(10000);
        }
        catch (Exception ex)
        {
          logger.LogInformation("CheckMempool failed: " + ex.Message);
        }
        await Task.Delay(new TimeSpan(0, 0, appSettings.MempoolCheckerIntervalSec.Value), stoppingToken);
      }
    }

    public async Task<bool> CheckMempoolAndResubmitTxs(int isBlockParserIdleMs)
    {
      var info = await blockChainInfo.GetInfoAsync();
      if (info.BestBlockHash != lastRefreshAtBlockHash || !success)
      {
        // wait for a while, if queue is idle then resubmit
        await Task.Delay(isBlockParserIdleMs);
        var blockparserStatus = blockParser.GetBlockParserStatus();
        if (blockparserStatus.BlocksQueued > 0 || blockparserStatus.LastBlockHash != info.BestBlockHash)
        {
          return true;
        }
        logger.LogDebug("CheckMempool: blockparser's eventbus looks idle, starting with resubmit...");
        var stopwatch = Stopwatch.StartNew();
        lastRefreshAtBlockHash = blockparserStatus.LastBlockHash;

        var (success, txsWithMissingInputs) = await mapi.ResubmitMissingTransactions();
        if (txsWithMissingInputs != null)
        {
          await ArrangeTxsWithMissingInputs(txsWithMissingInputs);
        }

        logger.LogDebug($"CheckMempool: resubmit finished with '{ nameof(success) }'={ success }, took { stopwatch.ElapsedMilliseconds } ms.");

        return success;
      }
      return true;
    }

    private async Task ArrangeTxsWithMissingInputs(List<long> txsWithMissingInputs)
    {
      Dictionary<long, int> txsRetriesIncremented = new();
      foreach (var tx in txsWithMissingInputs)
      {
        var retries = txsRetries.GetValueOrDefault(tx);
        txsRetriesIncremented[tx] = retries + 1;
      }
      var txsWithMax = txsRetriesIncremented.Where(x => x.Value == appSettings.MempoolCheckerMissingInputsRetries + 1).ToList();
      await txRepository.UpdateTxsOnResubmitAsync(Faults.DbFaultComponent.MempoolCheckerUpdateTxs, txsWithMax.Select(x => new Tx
      {
        TxInternalId = x.Key,
        SubmittedAt = clock.UtcNow(),
        TxStatus = TxStatus.MissingInputsMaxRetriesReached,
        UpdateTx = true
      }).ToList());
      txsWithMax.ForEach(x => txsRetriesIncremented.Remove(x.Key));
      txsRetries = txsRetriesIncremented;
    } 
  }
}
