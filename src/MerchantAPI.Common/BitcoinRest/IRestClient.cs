// Copyright (c) 2021 Bitcoin Association

using System.Threading;
using System.Threading.Tasks;

namespace MerchantAPI.Common.BitcoinRest
{
  public interface IRestClient
  {
    Task<byte[]> GetBlockAsBytesAsync(string blockHash, CancellationToken? token = null);
  }
}
