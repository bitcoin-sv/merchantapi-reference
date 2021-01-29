// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.Common.ExternalServices;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MerchantAPI.PaymentAggregator.Domain.ViewModels;

namespace MerchantAPI.PaymentAggregator.Domain.Client
{
  public class ApiGatewayClient : IApiGatewayClient
  {
    public string Url { get; set; }
    readonly IHttpClientFactory httpClientFactory;

    public ApiGatewayClient(string url, IHttpClientFactory httpClientFactory)
    {
      this.Url = url;
      this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
    }

    private RestClient CreateClient(string url)
    {
      // all calls are anonymous
      return new RestClient(url, null, httpClientFactory.CreateClient("APIGatewayClient"));
    }

    public async Task TestMapiFeeQuoteAsync(CancellationToken token)
    {
      string additionalUrl = "mapi/feeQuote";
      var client = CreateClient(this.Url);
      await client.GetStringAsync(additionalUrl, token: token);
    }

    public async Task<SignedPayloadViewModel> GetFeeQuoteAsync(CancellationToken token)
    {
      string additionalUrl = "mapi/feeQuote";
      var client = CreateClient(this.Url);
      var result = await client.GetStringAsync(additionalUrl, throwExceptionOn404: false, token : token);

      SignedPayloadViewModel response = JsonSerializer.Deserialize<SignedPayloadViewModel>(result);
      return response;
    }

    public async Task<SignedPayloadViewModel> QueryTransactionStatusAsync(string txId, CancellationToken token)
    {
      string additionalUrl = $"mapi/tx/{ txId }";
      var client = CreateClient(this.Url);
      var result = await client.GetStringAsync(additionalUrl, throwExceptionOn404: false, token: token);
      SignedPayloadViewModel response = JsonSerializer.Deserialize<SignedPayloadViewModel>(result);
      return response;
    }

    public async Task<SignedPayloadViewModel> SubmitTransactionAsync(string payload, CancellationToken token)
    {
      string additionalUrl = "mapi/tx";
      var client = CreateClient(this.Url);
      var result = await client.PostJsonAsync(additionalUrl, payload, throwExceptionOn404: false, token: token);
      SignedPayloadViewModel response = JsonSerializer.Deserialize<SignedPayloadViewModel>(result);
      return response;
    }

    public async Task<SignedPayloadViewModel> SubmitTransactionsAsync(string payload, CancellationToken token)
    {
      string additionalUrl = "mapi/txs";
      var client = CreateClient(this.Url);
      var result = await client.PostJsonAsync(additionalUrl, payload, throwExceptionOn404: false, token: token);
      SignedPayloadViewModel response = JsonSerializer.Deserialize<SignedPayloadViewModel>(result);
      return response;
    }

    public async Task<SignedPayloadViewModel> SubmitRawTransactionAsync(byte[] payload, string callbackUrl, string callbackToken, string callbackEncryption, bool merkleProof, bool dsCheck, CancellationToken token)
    {
      string queryParameters = $"callbackUrl={callbackUrl}&callbackToken={callbackToken}&callbackEncryption={callbackEncryption}&merkleProof={merkleProof}&dsCheck={dsCheck}";
      string additionalUrl = $"mapi/tx?{queryParameters}";
      var client = CreateClient(this.Url);
      var result = await client.PostOctetStream(additionalUrl, payload, throwExceptionOn404: false, token: token);
      SignedPayloadViewModel response = JsonSerializer.Deserialize<SignedPayloadViewModel>(result);
      return response;
    }

    public async Task<SignedPayloadViewModel> SubmitRawTransactionsAsync(byte[] payload, string callbackUrl, string callbackToken, string callbackEncryption, bool merkleProof, bool dsCheck, CancellationToken token)
    {
      string queryParameters = $"callbackUrl={callbackUrl}&callbackToken={callbackToken}&callbackEncryption={callbackEncryption}&merkleProof={merkleProof}&dsCheck={dsCheck}";
      string additionalUrl = $"mapi/txs?{queryParameters}";
      var client = CreateClient(this.Url);
      var result = await client.PostOctetStream(additionalUrl, payload, throwExceptionOn404: false, token: token);
      SignedPayloadViewModel response = JsonSerializer.Deserialize<SignedPayloadViewModel>(result);
      return response;
    }
  }
}
