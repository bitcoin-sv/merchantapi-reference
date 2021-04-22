// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;

namespace MerchantAPI.APIGateway.Domain.Models.Zmq
{
  public class ZmqEndpoint
  {
    public string Address { get; set; }

    public string[] Topics { get; set; }

    public DateTime LastPingAt { get; set; }

    public DateTime? LastMessageAt { get; set; }
  }
}
