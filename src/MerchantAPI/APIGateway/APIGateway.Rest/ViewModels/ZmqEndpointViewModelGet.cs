// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using System.Text.Json.Serialization;
using MerchantAPI.APIGateway.Domain.Models.Zmq;

namespace MerchantAPI.APIGateway.Rest.ViewModels
{
  public class ZmqEndpointViewModelGet
  {
    [JsonPropertyName("address")]
    public string Address { get; set; }

    [JsonPropertyName("topic")]
    public string[] Topics { get; set; }

    [JsonPropertyName("lastPingAt")]
    public DateTime LastPingAt { get; set; }

    [JsonPropertyName("lastMessageAt")]
    public DateTime? LastMessageAt { get; set; }

    public ZmqEndpointViewModelGet()
    { }

    public ZmqEndpointViewModelGet(ZmqEndpoint domain)
    {
      Address = domain.Address;
      Topics = domain.Topics;
      LastPingAt = domain.LastPingAt.ToUniversalTime();
      LastMessageAt = domain.LastMessageAt?.ToUniversalTime();
    }
  }
}