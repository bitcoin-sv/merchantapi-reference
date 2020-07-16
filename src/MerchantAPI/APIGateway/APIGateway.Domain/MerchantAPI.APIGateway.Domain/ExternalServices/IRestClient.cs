// Copyright (c) 2020 Bitcoin Association

using System;
using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Domain.ExternalServices
{
  public interface IRestClient
  {
    public string BaseURL { get;  }
    public string Authorization { get; }
    Task<string> PostJsonAsync(string additionalUrl, string jsonRequest, bool throwExceptionOn404 = true, TimeSpan? requestTimeout = null);

    Task<string> PostOctetStream(string additionalUrl, byte[] request, bool throwExceptionOn404 = true,
      TimeSpan? requestTimeout = null);
    Task<string> GetStringAsync(string additionalUrl, bool throwExceptionOn404 = true,
      TimeSpan? requestTimeout = null);

    

  }
}
