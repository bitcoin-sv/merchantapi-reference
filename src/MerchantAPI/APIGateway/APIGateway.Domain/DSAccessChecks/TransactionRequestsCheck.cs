// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain.Cache;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NBitcoin;
using System;
using System.Collections.Generic;

namespace MerchantAPI.APIGateway.Domain.DSAccessChecks
{
  public class TransactionRequestsCheck: ITransactionRequestsCheck
  {
    readonly MemoryCache txRequestsCache;
    readonly MemoryCache hostUnknownTxRequests;
    readonly IHostBanList banList;
    readonly ILogger<TransactionRequestsCheck> logger;
    readonly IDictionary<string, HashSet<uint256>> allowedTxSubmitList = new Dictionary<string, HashSet<uint256>>();

    readonly AppSettings appSettings;
    readonly MemoryCacheEntryOptions requestsEntryOptions;
    readonly MemoryCacheEntryOptions hostEntryOptions;

    public TransactionRequestsCheck(IOptions<AppSettings> options, TxRequestsMemoryCache requestsMemoryCache, HostUnknownTxCache hostUnknownTxCache, IHostBanList banList, ILogger<TransactionRequestsCheck> logger)
    {
      txRequestsCache = requestsMemoryCache.Cache ?? throw new ArgumentNullException(nameof(requestsMemoryCache));
      hostUnknownTxRequests = hostUnknownTxCache.Cache ?? throw new ArgumentNullException(nameof(hostUnknownTxCache));
      this.banList = banList ?? throw new ArgumentNullException(nameof(banList));
      this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
      appSettings = options.Value;
      requestsEntryOptions = new MemoryCacheEntryOptions
      {
        SlidingExpiration = TimeSpan.FromSeconds(appSettings.DSCachedTxRequestsCooldownPeriodSec),
        Size = 1
      };
      hostEntryOptions = new MemoryCacheEntryOptions
      {
        SlidingExpiration = TimeSpan.FromSeconds(appSettings.DSUnknownTxQueryCooldownPeriodSec),
        Size = 1
      };
    }

    public void LogKnownTransactionId(string host, uint256 transactionId)
    {
      lock (txRequestsCache)
      {
        var requestData = txRequestsCache.GetOrCreate(transactionId, entry =>
        {
          entry.SetOptions(requestsEntryOptions);

          var newData = new Dictionary<string, int>
          {
            { host, 0 }
          };
          return newData;
        });

        var requestCount = requestData[host];
        requestCount++;
        
        if (requestCount < appSettings.DSMaxNumOfTxQueries)
        {
          requestData[host] = requestCount;
          txRequestsCache.Set(transactionId, requestData, requestsEntryOptions);

          return;
        }

        // If there were too many queries for same txId, ban the host and remove it from the list, so that after cool-down it can try again.
        banList.IncreaseBanScore(host, HostBanList.BanScoreLimit);
        requestData.Remove(host);
        txRequestsCache.Set(transactionId, requestData, requestsEntryOptions);
        logger.LogInformation($"Banning host '{host}' for exceeding {appSettings.DSMaxNumOfTxQueries} concurrent calls for transaction '{transactionId}'.");
      }
    }

    public void LogUnknownTransactionRequest(string host)
    {
      lock (hostUnknownTxRequests)
      {
        var requestsCount = hostUnknownTxRequests.GetOrCreate(host, entry =>
        {
          entry.SetOptions(hostEntryOptions);

          return 0;
        });

        requestsCount++;
        if (requestsCount < appSettings.DSMaxNumOfUnknownTxQueries)
        {
          hostUnknownTxRequests.Set(host, requestsCount, hostEntryOptions);

          return;
        }
        
        // If too many queries for unknown Txids were made, we ban the host and remove it from the list 
        hostUnknownTxRequests.Remove(host);
        banList.IncreaseBanScore(host, HostBanList.BanScoreLimit);
        logger.LogInformation($"Banning host '{host}' for exceeding '{appSettings.DSMaxNumOfUnknownTxQueries}' queries for unknown transactions in '{appSettings.DSUnknownTxQueryCooldownPeriodSec}' seconds.");
      }
    }

    public void LogQueriedTransactionId(string host, uint256 txId)
    {
      lock(allowedTxSubmitList)
      {
        if (allowedTxSubmitList.TryGetValue(host, out var txList))
        {
          if (!txList.Contains(txId))
          {
            txList.Add(txId);
            allowedTxSubmitList[host] = txList;
          }
        }
        else
        {
          allowedTxSubmitList.Add(host, new HashSet<uint256> { txId });
        }
      }
    }

    public void RemoveQueriedTransactionId(string host, uint256 txId)
    {
      lock(allowedTxSubmitList)
      {
        var txList = allowedTxSubmitList[host];
        txList.Remove(txId);
        allowedTxSubmitList[host] = txList;
      }
    }

    public bool WasTransactionIdQueried(string host, uint256 txId)
    {
      if (!allowedTxSubmitList.ContainsKey(host) ||
          !allowedTxSubmitList[host].Contains(txId))
      {
        return false;
      }

      return true;
    }
  }
}
