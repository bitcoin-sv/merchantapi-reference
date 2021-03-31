// Copyright (c) 2020 Bitcoin Association

using Microsoft.Extensions.Caching.Memory;

namespace MerchantAPI.APIGateway.Domain.DSAccessChecks
{
  public class HostBanListMemoryCache
  {
    public MemoryCache Cache { get; set; }

    public HostBanListMemoryCache()
    {
      // We limit the number of hosts that can be banned for a given time
      Cache = new MemoryCache(new MemoryCacheOptions
      {
        SizeLimit = 100000
      });
    }
  }

  public class TxRequestsMemoryCache
  {
    public MemoryCache Cache { get; set; }

    public TxRequestsMemoryCache()
    {
      // We limit the number of transaction ids that were requested
      Cache = new MemoryCache(new MemoryCacheOptions
      {
        SizeLimit = 1000000
      });
    }
  }

  public class HostUnknownTxCache
  {
    public MemoryCache Cache { get; set; }

    public HostUnknownTxCache()
    {
      // We limit the number of transaction ids that were requested
      Cache = new MemoryCache(new MemoryCacheOptions
      {
        SizeLimit = 100000
      });
    }
  }
}
