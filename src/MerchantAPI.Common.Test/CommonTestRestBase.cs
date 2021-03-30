// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.Common.Authentication;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MerchantAPI.Common.Test
{
  public abstract class CommonTestRestBase<TAppSettings> : CommonTestBase<TAppSettings>
  where TAppSettings : CommonAppSettings, new()
  {
    public abstract string GetBaseUrl();

    HttpRequestMessage BuildHttpRequestMessage(HttpMethod httpMethod, string url, HttpContent content = null)
    {
      var requestMessage = new HttpRequestMessage(httpMethod, url);
      if (content != null)
      {
        requestMessage.Content = content;
      }

      if (!string.IsNullOrEmpty(RestAuthentication))
      {
        requestMessage.Headers.Add("Authorization", RestAuthentication);
      }
      if (!string.IsNullOrEmpty(ApiKeyAuthentication))
      {
        requestMessage.Headers.Add(ApiKeyAuthenticationHandler<TAppSettings>.ApiKeyHeaderName, ApiKeyAuthentication);
      }

      return requestMessage;
    }

    async Task<(TResponse response, HttpResponseMessage httpResponse)> ParseResponse<TResponse>(HttpResponseMessage httpResponse, HttpStatusCode expectedStatusCode) where TResponse : class
    {

      if (expectedStatusCode != httpResponse.StatusCode)
      {
        // include body in assert message to make debugging easier
        var body = await httpResponse.Content.ReadAsStringAsync();
        Assert.AreEqual(expectedStatusCode, httpResponse.StatusCode, "body: " + body);
      }


      string responseString = await httpResponse.Content.ReadAsStringAsync();
      TResponse response = null;

      if (httpResponse.IsSuccessStatusCode) // Only try to deserialize in case there are no exception
      {
        response = JsonSerializer.Deserialize<TResponse>(responseString);
      }

      return (response, httpResponse);
    }

    public async Task<HttpResponseMessage> PerformRequestAsync(HttpClient client, HttpMethod httpMethod, string uri, HttpContent content = null)
    {
      var reqMessage = BuildHttpRequestMessage(httpMethod, uri, content);
      return await client.SendAsync(reqMessage);
    }

    public async Task<HttpResponseMessage> Put<TRequest>(HttpClient client,
      string uri,
      TRequest request,
      HttpStatusCode expectedStatusCode)
    {
      var httpResponse = await PerformRequestAsync(client, HttpMethod.Put, uri,
        new StringContent(JsonSerializer.Serialize(request),
          Encoding.UTF8, "application/json"));

      Assert.AreEqual(expectedStatusCode, httpResponse.StatusCode);

      return httpResponse;
    }


    public async Task Delete(HttpClient client, string uri, HttpStatusCode expectedStatusCode = HttpStatusCode.NoContent)
    {
      var httpResponse = await PerformRequestAsync(client, HttpMethod.Delete, uri);

      // Delete always return NoContent to make (response) idempotent
      Assert.AreEqual(expectedStatusCode, httpResponse.StatusCode);
    }

    public async Task<(TResponse response, HttpResponseMessage httpResponse)> GetWithHttpResponseReturned<TResponse>(
          HttpClient client,
          string url,
          HttpStatusCode expectedStatusCode)
      where TResponse : class
    {
      var httpResponse = await PerformRequestAsync(client, HttpMethod.Get, url);

      return await ParseResponse<TResponse>(httpResponse, expectedStatusCode);
    }

    public async Task<TResponse> Get<TResponse>(
          HttpClient client, 
          string uri, 
          HttpStatusCode expectedStatusCode)
      where TResponse : class
    {
      var httpResponse = await PerformRequestAsync(client, HttpMethod.Get, uri);

      Assert.AreEqual(expectedStatusCode, httpResponse.StatusCode);

      string responseString = await httpResponse.Content.ReadAsStringAsync();
      if (string.IsNullOrEmpty(responseString))
      {
        return default;
      }

      TResponse response = null;
      if (httpResponse.IsSuccessStatusCode) // Only try to deserialize in case there are no exception
      {
        response = JsonSerializer.Deserialize<TResponse>(responseString);
      }
      return response;
    }

    public async Task<(TResponse response, HttpResponseMessage httpResponse)> Post<TRequest, TResponse>(
            HttpClient client,
            TRequest request,
            HttpStatusCode expectedStatusCode)
            where TResponse : class
    {
      return await Post<TResponse>(
        GetBaseUrl(),
        client,
        new StringContent(JsonSerializer.Serialize(request),
          Encoding.UTF8, "application/json"),
        expectedStatusCode
      );
    }

    public async Task<(TResponse response, HttpResponseMessage httpResponse)> Post<TResponse>(
          string url,
          HttpClient client,
          ByteArrayContent requestContent,
          HttpStatusCode expectedStatusCode)
          where TResponse : class
    {
      var httpResponse = await PerformRequestAsync(client, HttpMethod.Post, url, requestContent);

      return await ParseResponse<TResponse>(httpResponse, expectedStatusCode);
    }

    public string PrepareQueryParams(string url, IEnumerable<(string Name, string Value)> queryParams)
    {
      return url + "?" + string.Join("&", queryParams.Select(x => x.Name + "=" + x.Value));
    }
  }
}
