// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using System.Net.Http;
using Microsoft.Extensions.Logging;

namespace MerchantAPI.Common.BitcoinRpc
{
  public class RpcClientFactory : IRpcClientFactory
  {
    readonly ILogger<RpcClient> logger;
    readonly IHttpClientFactory httpClientFactory;

    public RpcClientFactory(IHttpClientFactory httpClientFactory, ILogger<RpcClient> logger)
    {
      this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
      this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    public IRpcClient Create(string host, int port, string username, string password)
    {
      return new RpcClient(CreateAddress(host, port), new System.Net.NetworkCredential(username, password), logger, httpClientFactory.CreateClient(host));
    }

    public IRpcClient Create(string host, int port, string username, string password, int requestTimeoutSec, int multiRequestTimeoutSec, int numOfRetries, int waitBetweenRetriesMs)
    {
      return new RpcClient(CreateAddress(host, port), new System.Net.NetworkCredential(username, password), logger, httpClientFactory.CreateClient(host),
        requestTimeoutSec, multiRequestTimeoutSec, numOfRetries, waitBetweenRetriesMs);
    }

    public static  Uri CreateAddress(string host, int port)
    {
      UriBuilder builder = new()
      {
        Host = host,
        Scheme = "http",
        Port = port
      };
      return builder.Uri;
    }

  }
}
