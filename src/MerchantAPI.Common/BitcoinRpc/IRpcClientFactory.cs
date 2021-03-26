// Copyright (c) 2020 Bitcoin Association

using System.Net.Http;

namespace MerchantAPI.Common.BitcoinRpc
{
  public interface IRpcClientFactory
  { 
    IRpcClient Create(string host, int port, string username, string password, IHttpClientFactory httpClientFactory);
  }
}
