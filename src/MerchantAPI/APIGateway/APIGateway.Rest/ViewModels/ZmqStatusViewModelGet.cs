// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using System.Linq;
using System.Text.Json.Serialization;
using MerchantAPI.APIGateway.Domain.Models;
using MerchantAPI.APIGateway.Domain.Models.Zmq;

namespace MerchantAPI.APIGateway.Rest.ViewModels
{
  public class ZmqStatusViewModelGet
  {
    [JsonPropertyName("nodeId")]
    public string NodeId { get; set; } // Host + port

    [JsonPropertyName("endpoints")]
    public ZmqEndpointViewModelGet[] Endpoints { get; set; }

    [JsonPropertyName("isResponding")]
    public bool IsResponding { get; set; }

    [JsonPropertyName("lastConnectionAttemptAt")]
    public DateTime? LastConnectionAttemptAt { get; set; }

    [JsonPropertyName("lastError")]
    public string LastError { get; set; }

    public ZmqStatusViewModelGet()
    { }

    public ZmqStatusViewModelGet(Node domainNode, ZmqStatus domainZmqStatus)
    {
      NodeId = domainNode.ToExternalId();
      IsResponding = domainZmqStatus.IsResponding;
      LastConnectionAttemptAt = domainZmqStatus.LastConnectionAttemptAt?.ToUniversalTime();
      LastError = domainZmqStatus.LastError ?? "";
      if (domainZmqStatus.Endpoints != null)
        Endpoints = domainZmqStatus.Endpoints.Select(e => new ZmqEndpointViewModelGet(e)).ToArray();
    }
  }
}
