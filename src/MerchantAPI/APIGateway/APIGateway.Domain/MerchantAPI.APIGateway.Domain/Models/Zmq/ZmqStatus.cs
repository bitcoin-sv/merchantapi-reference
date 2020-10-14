// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;
using System.Text;

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
