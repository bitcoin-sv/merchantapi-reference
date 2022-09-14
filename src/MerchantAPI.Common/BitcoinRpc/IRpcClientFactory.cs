// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;

namespace MerchantAPI.Common.BitcoinRpc
{
  public interface IRpcClientFactory
  { 
    IRpcClient Create(string host, int port, string username, string password);
    IRpcClient Create(
      string host,
      int port,
      string username,
      string password,
      int requestTimeoutSec,
      int multiRequestTimeoutSec,
      int numOfRetries,
      int waitBetweenRetriesMs);
  }
}
