// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;
using System.Text;

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
