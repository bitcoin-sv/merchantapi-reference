// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.Common;
using MerchantAPI.Common.Exceptions;
using MerchantAPI.PaymentAggregator.Domain.Models;
using MerchantAPI.PaymentAggregator.Domain.ViewModels;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MerchantAPI.PaymentAggregator.Domain.Client
{
  public class ApiGatewayMultiClient : IApiGatewayMultiClient
  {
    IGateways gateways;
    IApiGatewayClientFactory apiGatewayClientFactory;
    private ILogger logger;

    public ApiGatewayMultiClient(IGateways gateways, IApiGatewayClientFactory apiGatewayClientFactory, ILogger<ApiGatewayMultiClient> logger)
    {
      this.gateways = gateways ?? throw new ArgumentNullException(nameof(gateways));
      this.apiGatewayClientFactory = apiGatewayClientFactory ?? throw new ArgumentNullException(nameof(apiGatewayClientFactory));
      this.logger = logger;
    }

    IApiGatewayClient[] GetApiGatewayClients()
    {
      var result = gateways.GetGateways(true).Select(
        g => apiGatewayClientFactory.Create(g.Url)).ToArray();

      if (!result.Any())
      {
        throw new Exception("No gateways available");
      }

      return result;
    }

    async Task<Task<T>[]> GetAll<T>(Func<IApiGatewayClient, Task<T>> call)
    {
      var apiGatewayClients = GetApiGatewayClients();

      Task<T>[] tasks = apiGatewayClients.Select(x =>
      {
        try
        {
          return call(x);
        }
        catch (Exception e)
        {
          // Catch exceptions that can happen if call is implemented synchronously
          return Task.FromException<T>(e);
        }
      }

      ).ToArray();
      try
      {
        await Task.WhenAll(tasks);
      }
      catch (Exception e)
      {
        logger.LogError(
          $"Error while calling gateway.  Error: {e}");
      }

      return tasks;

    }

    async Task<T[]> GetAllWithoutErrors<T>(Func<IApiGatewayClient, Task<T>> call, bool throwIfEmpty = true)
    {
      var tasks = await GetAll(call);

      var successful = tasks.Where(t => t.IsCompletedSuccessfully).Select(t => t.Result).ToArray();

      if (throwIfEmpty && !successful.Any())
      {
        throw new ServiceUnavailableException($"None of the gateways returned successful response. First error: {tasks[0].Exception?.Message} ");
      }

      return successful;
    }

    public Task<SignedPayloadViewModel[]> GetFeeQuoteAsync(CancellationToken token)
    {
      return GetAllWithoutErrors(x => x.GetFeeQuoteAsync(token));
    }

    public Task<SignedPayloadViewModel[]> QueryTransactionStatusAsync(string txId, CancellationToken token)
    {
      return GetAllWithoutErrors(x => x.QueryTransactionStatusAsync(txId, token));
    }

    public Task<SignedPayloadViewModel[]> SubmitTransactionAsync(string payload, CancellationToken token)
    {
      return GetAllWithoutErrors(x => x.SubmitTransactionAsync(payload, token));
    }

    public Task<SignedPayloadViewModel[]> SubmitTransactionsAsync(string payload, CancellationToken token)
    {
      return GetAllWithoutErrors(x => x.SubmitTransactionsAsync(payload, token));
    }

    public Task<SignedPayloadViewModel[]> SubmitRawTransactionAsync(byte[] payload, string callbackUrl, string callbackToken, string callbackEncryption, bool merkleProof, bool dsCheck, CancellationToken token)
    {
      return GetAllWithoutErrors(x => x.SubmitRawTransactionAsync(payload, callbackUrl, callbackToken, callbackEncryption, merkleProof, dsCheck, token));
    }

    public Task<SignedPayloadViewModel[]> SubmitRawTransactionsAsync(byte[] payload, string callbackUrl, string callbackToken, string callbackEncryption, bool merkleProof, bool dsCheck, CancellationToken token)
    {
      return GetAllWithoutErrors(x => x.SubmitRawTransactionsAsync(payload, callbackUrl, callbackToken, callbackEncryption, merkleProof, dsCheck, token));
    }
  }
}
