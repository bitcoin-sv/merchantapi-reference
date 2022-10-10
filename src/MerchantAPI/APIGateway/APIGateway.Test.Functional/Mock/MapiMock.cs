// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain;
using MerchantAPI.APIGateway.Domain.Actions;
using MerchantAPI.APIGateway.Domain.Metrics;
using MerchantAPI.APIGateway.Domain.Models;
using MerchantAPI.APIGateway.Domain.Models.Faults;
using MerchantAPI.APIGateway.Domain.Repositories;
using MerchantAPI.Common.BitcoinRpc.Responses;
using MerchantAPI.Common.Clock;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Test.Functional.Mock
{
  public class MapiMock : Mapi
  {
    public MapiMock(
      IRpcMultiClient rpcMultiClient,
      IFeeQuoteRepository feeQuoteRepository,
      IBlockChainInfo blockChainInfo,
      IMinerId minerId,
      ITxRepository txRepository,
      ILogger<Mapi> logger,
      IClock clock,
      IOptions<AppSettings> appSettingOptions,
      IFaultManager faultManager,
      IFaultInjection faultInjection,
      MapiMetrics mapiMetrics,
      MempoolCheckerMetrics mempoolCheckerMetrics)
      : base(rpcMultiClient, feeQuoteRepository, blockChainInfo, minerId, txRepository, logger, clock, appSettingOptions, faultManager, faultInjection, mapiMetrics, mempoolCheckerMetrics)
    {
    }

    public void SimulateMode(Faults.SimulateSendTxsResponse newMode, Faults.FaultType faultType = Faults.FaultType.SimulateSendTxsMapi)
    {
      if (faultType == Faults.FaultType.DbAfterSavingUncommittedState ||
          faultType == Faults.FaultType.DbBeforeSavingUncommittedState)
      {
        throw new Exception("Invalid faultType for SimulateMode.");
      }

      faultManager.Clear();

      FaultTrigger trigger = new()
      {
        Type = faultType,
        SimulateSendTxsResponse = newMode
      };

      faultManager.Add(trigger);
    }

    public void SimulateDbFault(
      Faults.FaultType faultType,
      Faults.DbFaultComponent faultComponent,
      Faults.DbFaultMethod faultMethod = Faults.DbFaultMethod.Exception)
    {
      if (faultType != Faults.FaultType.DbAfterSavingUncommittedState &&
          faultType != Faults.FaultType.DbBeforeSavingUncommittedState)
      {
        throw new Exception("Invalid faultType for SimulateDbFault.");
      }
      faultManager.Clear();

      FaultTrigger trigger = new()
      {
        Type = faultType,
        DbFaultComponent = faultComponent,
        DbFaultMethod = faultMethod
      };

      faultManager.Add(trigger);
    }

    public void ClearMode()
    {
      faultManager.Clear();
    }

    public override async Task<(RpcSendTransactions, Exception)> SendRawTransactions(
    (byte[] transaction, bool allowhighfees, bool dontCheckFees, bool listUnconfirmedAncestors, Dictionary<string, object> config)[] transactions,
    Faults.FaultType faultType)
    {
      return await base.SendRawTransactions(transactions, faultType);
    }

    public async Task<(bool success, List<long> txsWithMissingInputs)> ResubmitMissingTransactions(string[] mempoolTxs, int batchSize = 100)
    {
      return await base.ResubmitMissingTransactionsAsync(mempoolTxs, null, batchSize);
    }

    public override async Task<(bool success, List<long> txsWithMissingInputs)> ResubmitMissingTransactionsAsync(string[] mempoolTxs, DateTime? resubmittedAt, int batchSize = 100)
    {
      return await base.ResubmitMissingTransactionsAsync(mempoolTxs, resubmittedAt, batchSize);
    }
  }
}
