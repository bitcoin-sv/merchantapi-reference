// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using Microsoft.Extensions.Caching.Memory;

namespace MerchantAPI.APIGateway.Infrastructure.Cache
{
  public class PrevTxOutputCache
  {
    public MemoryCache Cache { get; set; }

    public PrevTxOutputCache()
    {
      // We limit the number of tx outs
      Cache = new MemoryCache(new MemoryCacheOptions
      {
        SizeLimit = 500000
      });
    }
  }
}
