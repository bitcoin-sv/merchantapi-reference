// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using System.Threading;
using System.Threading.Tasks;

namespace MerchantAPI.Common.ExternalServices
{
  public interface IRestClient
  {
    public string BaseURL { get;  }
    public string Authorization { get; }
    Task<string> PostJsonAsync(string additionalUrl, string jsonRequest, bool throwExceptionOn404 = true, 
      TimeSpan? requestTimeout = null, CancellationToken token = default);

    Task<string> PostOctetStream(string additionalUrl, byte[] request, bool throwExceptionOn404 = true,
      TimeSpan? requestTimeout = null, CancellationToken token = default);
    Task<string> GetStringAsync(string additionalUrl, bool throwExceptionOn404 = true,
      TimeSpan? requestTimeout = null, CancellationToken token = default);

    

  }
}
