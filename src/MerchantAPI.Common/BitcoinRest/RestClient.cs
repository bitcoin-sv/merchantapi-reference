// Copyright (c) 2021 Bitcoin Association

using MerchantAPI.Common.Json;
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MerchantAPI.Common.BitcoinRest
{
  public class RestClient : IRestClient
  {
    private readonly Uri Address;

    private const int WaitBetweenRetriesMs = 100;

    public int NumOfRetries { get; set; } = 50;

    private HttpClient HttpClient { get; set; }

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(100);

    public RestClient(HttpClient httpClient, Uri address)
    {
      this.Address = address;
      HttpClient = httpClient;
    }

    public async Task<byte[]> GetBlockAsBytesAsync(string blockHash, CancellationToken? token = null)
    {
      var response = await RequestWithRetryAsync<string>(token, "block", $"{blockHash}.hex");
      return await HelperTools.HexStringToByteArrayAsync(response);
    }

    private async Task<Stream> RequestWithRetryAsync<T>(CancellationToken? token, string method, string param)
    {
      int retriesLeft = NumOfRetries;
      do
      {
        try
        {
          retriesLeft--;
          var rpcResponse = await MakeHttpRequestAsync<T>(token, method, param);
          return await rpcResponse.Content.ReadAsStreamAsync();

        }
        catch (TaskCanceledException ex)
        {
          throw new RestException($"REST call to {method} has been canceled", Address.AbsoluteUri, ex);
        }
        catch (Exception ex)
        {
          if (retriesLeft == 0)
          {
            throw new RestException($"Failed after {NumOfRetries} retries. Last error: {ex.Message}", Address.AbsoluteUri, ex);
          }
        }
        if (token.HasValue)
        {
          await Task.Delay(WaitBetweenRetriesMs, token.Value);
        }
        else
        {
          await Task.Delay(WaitBetweenRetriesMs);
        }

      } while (retriesLeft > 0);

      // Should not happen since we exit when retriesLeft == 0
      throw new Exception("Internal error RequestAsyncWithRetry reached the end");
    }

    private async Task<HttpResponseMessage> MakeHttpRequestAsync<T>(CancellationToken? token, string method, string param)
    {
      var reqMessage = CreateRequestMessage(method, param);
      using var cts = new CancellationTokenSource(RequestTimeout);
      using var cts2 = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, token ?? CancellationToken.None);

      var httpResponse = await HttpClient.SendAsync(reqMessage, cts2.Token);
      if (!httpResponse.IsSuccessStatusCode)
      {
        throw new RestException((int)httpResponse.StatusCode, $"Error calling bitcoin REST ({Address.AbsoluteUri}). {httpResponse.ReasonPhrase}", Address.AbsoluteUri);
      }
      return httpResponse;
    }

    private HttpRequestMessage CreateRequestMessage(string method, string param)
    {
      var reqMessage = new HttpRequestMessage(HttpMethod.Get, Address.AbsoluteUri + $"rest/{method}/{param}");
      return reqMessage;
    }
  }
}
