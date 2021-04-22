// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;

namespace MerchantAPI.APIGateway.Domain.Models.Zmq
{
  public class ZmqStatus
  {
    public ZmqEndpoint[] Endpoints { get; set; }

    public bool IsResponding { get; set; }

    public DateTime? LastConnectionAttemptAt { get; set; }

    public string LastError { get; set; }
  }
}
