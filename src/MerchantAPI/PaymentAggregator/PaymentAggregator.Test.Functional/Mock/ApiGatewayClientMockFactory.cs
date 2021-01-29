// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.Common;
using MerchantAPI.Common.Clock;
using MerchantAPI.PaymentAggregator.Domain.Client;
using MerchantAPI.PaymentAggregator.Domain.Repositories;
using System;

namespace MerchantAPI.PaymentAggregator.Test.Functional.Mock
{
  public class ApiGatewayClientMockFactory : IApiGatewayClientFactory
  {
    readonly IFeeQuoteRepository feeQuoteRepository;
    readonly IClock clock;

    public ApiGatewayClientMockFactory(IFeeQuoteRepository feeQuoteRepository, IClock clock)
    {
      this.feeQuoteRepository = feeQuoteRepository ?? throw new ArgumentNullException(nameof(feeQuoteRepository));
      this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public IApiGatewayClient Create(string url)
    {
      return new ApiGatewayClientMock(url, feeQuoteRepository, clock);
    }
  }
}
