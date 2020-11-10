// Copyright (c) 2020 Bitcoin Association

namespace MerchantAPI.PaymentAggregator.Domain.Client
{
  public interface IApiGatewayClientFactory
  {
    IApiGatewayClient Create(string url);
  }
}
