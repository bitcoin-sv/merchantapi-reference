// Copyright (c) 2020 Bitcoin Association

using System;
using System.Net.Http;

namespace MerchantAPI.PaymentAggregator.Domain.Client
{
  public class ApiGatewayClientFactory : IApiGatewayClientFactory
  {
    readonly IHttpClientFactory httpClientFactory;

    public ApiGatewayClientFactory(IHttpClientFactory httpClientFactory)
    {
      this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    public IApiGatewayClient Create(string url)
    {
      return new ApiGatewayClient(url, httpClientFactory);
    }
  }
}
