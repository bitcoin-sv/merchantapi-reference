// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.APIGateway.Domain.Models;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MerchantAPI.APIGateway.Rest.ViewModels  // used for PUT
{
  public class NodeViewModelPut
  {
    [JsonIgnore]
    public string Id { get; set; }

    [JsonPropertyName("username")]
    [Required]
    public string Username { get; set; }

    [JsonPropertyName("password")]
    [Required]
    public string Password { get; set; }

    [JsonPropertyName("remarks")]
    public string Remarks { get; set; }


    public Node ToDomainObject()
    {
      var (host, port) = Node.SplitHostAndPort(Id);
      return new Node(
        host, 
        port,
        Username,
        Password,
        Remarks);
    }
  }
}
