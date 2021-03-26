﻿// Copyright (c) 2020 Bitcoin Association

using System;
using System.Net.Http;
using Microsoft.Extensions.Logging;

namespace MerchantAPI.Common.BitcoinRpc
{
  public class RpcClientFactory : IRpcClientFactory
  {
     ILogger<RpcClient> logger;
    public RpcClientFactory(ILogger<RpcClient> logger)
    {
      this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    public IRpcClient Create(string host, int port, string username, string password, IHttpClientFactory httpClientFactory)
    {
      return new RpcClient(CreateAddress(host, port), new System.Net.NetworkCredential(username, password), logger, httpClientFactory.CreateClient(host));
    }

    public static  Uri CreateAddress(string host, int port)
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
