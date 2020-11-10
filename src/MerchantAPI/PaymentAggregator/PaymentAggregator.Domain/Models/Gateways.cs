// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MerchantAPI.Common;
using MerchantAPI.Common.Clock;
using MerchantAPI.PaymentAggregator.Domain.Client;
using MerchantAPI.PaymentAggregator.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace MerchantAPI.PaymentAggregator.Domain.Models
{
  public class Gateways: IGateways
  {

    readonly IGatewayRepository GatewayRepository;
    readonly IApiGatewayClientFactory apiGatewayClientFactory;
    readonly ILogger<Gateways> logger;

    public Gateways(IGatewayRepository GatewayRepository,
      IClock clock,
      IApiGatewayClientFactory apiGatewayClientFactory,
      ILogger<Gateways> logger
    )
    {
      this.GatewayRepository = GatewayRepository ?? throw new ArgumentNullException(nameof(GatewayRepository));
      this.apiGatewayClientFactory = apiGatewayClientFactory ?? throw new ArgumentNullException(nameof(apiGatewayClientFactory));
      this.logger = logger;
    }


    public async Task<Gateway> CreateGatewayAsync(Gateway gateway)
    {
      logger.LogDebug($"Adding gateway {gateway}");

      try
      {
        // test call public getFeeQuote
        using CancellationTokenSource cts = new CancellationTokenSource(2000);
        await apiGatewayClientFactory.Create(gateway.Url).TestMapiFeeQuoteAsync(cts.Token); 
      }
      catch (Exception ex)
      {
        throw new BadRequestException($"The gateway was not added. Unable to connect to gateway with url: {gateway.Url}.", ex);
      }

      var createdGateway = GatewayRepository.CreateGateway(gateway);
      return createdGateway;
    }

    public async Task<bool> UpdateGatewayAsync(Gateway gateway)
    {
      try
      {
        // test call public getFeeQuote
        using CancellationTokenSource cts = new CancellationTokenSource(2000);
        await apiGatewayClientFactory.Create(gateway.Url).TestMapiFeeQuoteAsync(cts.Token);
      }
      catch (Exception ex)
      {
        throw new BadRequestException($"The gateway was not updated. Unable to connect to gateway with url: {gateway.Url}.", ex);
      }
      return GatewayRepository.UpdateGateway(gateway);
    }

    public IEnumerable<Gateway> GetGateways(bool onlyActive)
    {
      return GatewayRepository.GetGateways(onlyActive);
    }

    public Gateway GetGateway(int id)
    {
      return GatewayRepository.GetGateway(id);
    }

    public int DeleteGateway(int id)
    {
      logger.LogInformation($"Removing gateway id={id}");
      return GatewayRepository.DeleteGateway(id);
    }
  }
}
