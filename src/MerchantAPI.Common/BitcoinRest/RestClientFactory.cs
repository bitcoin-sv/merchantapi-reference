// Copyright (c) 2020 Bitcoin Association

using System;
using System.Net.Http;
using Microsoft.Extensions.Logging;

namespace MerchantAPI.Common.BitcoinRest
{
  public class RestClientFactory : IRestClientFactory
  {   
    IHttpClientFactory httpClientFactory;

    public RestClientFactory(IHttpClientFactory httpClientFactory)
    {
      this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));   
    }
    public IRestClient Create(string host, int port)
    {
      return new RestClient(httpClientFactory.CreateClient(host), CreateAddress(host, port));
    }

    public static Uri CreateAddress(string host, int port)
    {
      UriBuilder builder = new UriBuilder
      {
        Host = host,
        Scheme = "http",
        Port = port
      };
      return builder.Uri;
    }
  }
}
