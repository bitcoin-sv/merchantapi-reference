// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

namespace MerchantAPI.Common.BitcoinRpc
{
  public interface IRpcClientFactory
  { 
    IRpcClient Create(string host, int port, string username, string password);
  }
}
