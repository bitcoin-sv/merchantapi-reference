// Copyright (c) 2020 Bitcoin Association

using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MerchantAPI.Common;
using MerchantAPI.Common.Json;
using Microsoft.AspNetCore.Mvc;

namespace MerchantAPI.APIGateway.Domain.ExternalServices
{
  public class RestClient : IRestClient
  {
    static readonly TimeSpan defaultRequestTimeout = TimeSpan.FromSeconds(100);

    private readonly HttpClient httpClient;
    

    public RestClient(string baseUrl, string authorization, HttpClient httpClient)
    {
      this.BaseURL = baseUrl;
      this.Authorization = authorization;
      this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public string BaseURL { get;  }
    public string Authorization { get;  }

    private HttpRequestMessage CreateRequestMessage(HttpMethod httpMethod, string additionalUrl, HttpContent content = null)
    {
      var reqMessage = new HttpRequestMessage(httpMethod, new Uri(BaseURL + additionalUrl) );
      if (!string.IsNullOrEmpty(Authorization))
      {
        reqMessage.Headers.Add("Authorization",  Authorization);
      }

      if (content != null)
      {
        reqMessage.Content = content;
      }
      return reqMessage;
    }

    async Task<string> PerformRequest(HttpMethod httpMethod, string additionalUrl, HttpContent content , bool throwExceptionOn404 = true, TimeSpan? requestTimeout = null)
    {
      var reqMessage = CreateRequestMessage(httpMethod, additionalUrl, content);

      HttpResponseMessage httpResponse;
      string response;
      using (var cts = new CancellationTokenSource(requestTimeout ?? defaultRequestTimeout))
      {
        httpResponse = await httpClient.SendAsync(reqMessage, cts.Token);
        response = await httpResponse.Content.ReadAsStringAsync();

        if (!throwExceptionOn404 && httpResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
          return null;
        }
      }

      if (!httpResponse.IsSuccessStatusCode)
      {
        ProblemDetails problemDetails = null;
        try
        {
          problemDetails = HelperTools.JSONDeserializeNewtonsoft<ProblemDetails>(response);
        }
        catch (Exception) 
        {
          // We can ignore exception here. If there was an exception, problemDetails will be null and it will be handled later in the code.
        }

        string errMessage;
        if (problemDetails != null)
        {
          errMessage = $"Error calling {reqMessage.RequestUri}. Response code: {problemDetails.Status}, content: '{problemDetails.Title}'";
        }
        else
        {
          errMessage = $"Error calling {reqMessage.RequestUri}. Response code: {(int)httpResponse.StatusCode}, content: '{response}'";

        }
        if (httpResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
          throw new NotFoundException(errMessage);
        }

        throw new HttpRequestException(errMessage);
      }
      return response;
    }

    public async Task<string> GetStringAsync(string additionalUrl, bool throwExceptionOn404 = true,
      TimeSpan? requestTimeout = null)
    {
      var response = await PerformRequest(HttpMethod.Get, additionalUrl,
        null, throwExceptionOn404, requestTimeout);
      return response;
    }

    public async  Task<string> PostJsonAsync(string additionalUrl, string jsonRequest, bool throwExceptionOn404 = true, TimeSpan? requestTimeout = null)
    {
      var response = await PerformRequest(HttpMethod.Post,
        additionalUrl,
        new StringContent(jsonRequest, new UTF8Encoding(false), MediaTypeNames.Application.Json), throwExceptionOn404,
        requestTimeout);
      return response;
    }

    public Task<string> PostOctetStream(string additionalUrl, byte[] request, bool throwExceptionOn404 = true, TimeSpan? requestTimeout = null)
    {
      var content = new ByteArrayContent(request);
      content.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Octet);

      return PerformRequest(HttpMethod.Post,
           additionalUrl,
           content , throwExceptionOn404,
           requestTimeout);
      ;
    }
  }
}
