// Copyright (c) 2020 Bitcoin Association

namespace MerchantAPI.Common.BitcoinRpc
{
  public interface IRpcClientFactory
  { 
    IRpcClient Create(string host, int port, string username, string password);
  }
}
