﻿// Copyright(c) 2021 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using System.Net.Sockets;

namespace MerchantAPI.APIGateway.Domain.Models
{
  public class ZMQEndpointChecker : IZMQEndpointChecker
  {
    private const int TCP_RESPONSE_TIMEOUT_SECONDS = 5;

    public bool IsZMQNotificationsEndpointReachable(string ZMQNotificationsEndpoint)
    {
      if (Uri.TryCreate(ZMQNotificationsEndpoint, UriKind.Absolute, out Uri validatedUri))
      {
        var open = IsPortOpen(validatedUri.Host, validatedUri.Port, TimeSpan.FromSeconds(TCP_RESPONSE_TIMEOUT_SECONDS));
        return open;
      }
      return false;
    }

    static bool IsPortOpen(string host, int port, TimeSpan timeout)
    {
      try
      {
        using var client = new TcpClient();
        var result = client.BeginConnect(host, port, null, null);
        var success = result.AsyncWaitHandle.WaitOne(timeout);
        client.EndConnect(result);
        return success;
      }
      catch
      {
        return false;
      }
    }
  }
}
