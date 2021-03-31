// Copyright (c) 2020 Bitcoin Association

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System;

namespace MerchantAPI.APIGateway.Domain.DSAccessChecks
{
  public class HostBanList : IHostBanList
  {
    public const int BanScoreLimit = 100;
    public const int WarningScore = 10;

    readonly MemoryCache hostBanList;
    readonly MemoryCacheEntryOptions entryOptions;

    public HostBanList(IOptions<AppSettings> options, HostBanListMemoryCache memoryCache)
    {
      hostBanList = memoryCache.Cache ?? throw new ArgumentNullException(nameof(memoryCache));
      entryOptions = new MemoryCacheEntryOptions
      {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(options.Value.DSHostBanTimeSec),
        Size = 1
      };
    }

    public void IncreaseBanScore(string host, int banScore)
    {
      lock (hostBanList)
      {
        if (hostBanList.TryGetValue(host, out int oldBanScore))
        {
          banScore += oldBanScore;
        }
        hostBanList.Set(host, banScore, entryOptions);
      }
    }

    public bool IsHostBanned(string host)
    {
      if (hostBanList.TryGetValue(host, out int banScore))
      {
        return banScore >= BanScoreLimit;
      }
      return false;
    }

    #region Following methods should only be used for testing
    
    public void RemoveFromBanList(string host)
    {
      hostBanList.Remove(host);
    }

    public int ReturnBanScore(string host)
    {
      return (int)hostBanList.Get(host);
    }
    
    #endregion
  }
}
