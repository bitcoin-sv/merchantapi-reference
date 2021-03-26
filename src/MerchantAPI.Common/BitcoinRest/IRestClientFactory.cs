// Copyright (c) 2020 Bitcoin Association

namespace MerchantAPI.Common.BitcoinRest
{
  public interface IRestClientFactory
  {
    IRestClient Create(string host, int port);
  }
}
