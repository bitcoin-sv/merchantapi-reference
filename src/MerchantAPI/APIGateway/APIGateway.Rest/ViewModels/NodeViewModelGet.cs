// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain.Models;
using System;
using System.Text.Json.Serialization;

namespace MerchantAPI.APIGateway.Rest.ViewModels
{
  public class NodeViewModelGet
  {
    [JsonPropertyName("id")]
    public string Id { get; set; } // Host + port

    [JsonPropertyName("username")]
    public string Username { get; set; }

    // For security reason, we never return password
    //[JsonPropertyName("password")]
    //public string Password { get; set; }

    [JsonPropertyName("remarks")]
    public string Remarks { get; set; }

    [JsonPropertyName("status")]
    public NodeStatus Status { get; set; }

    [JsonPropertyName("lastError")]
    public string LastError { get; set; }

    [JsonPropertyName("lastErrorAt")]
    public DateTime? LastErrorAt { get; set; }

    [JsonPropertyName("ZMQNotificationsEndpoint")]
    public string ZMQNotificationsEndpoint { get; set; }

    public NodeViewModelGet()
    { }

    public NodeViewModelGet(Node domain)
    {
      Id = domain.ToExternalId();
      Username = domain.Username;
      //Password = domain.Password;
      Remarks = domain.Remarks;
      ZMQNotificationsEndpoint = domain.ZMQNotificationsEndpoint;
      Status = domain.Status;
      LastError = domain.LastError;
      LastErrorAt = domain.LastErrorAt;
    }
  }
}
