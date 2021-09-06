// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.APIGateway.Domain.Models;
using MerchantAPI.Common.Validation;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MerchantAPI.APIGateway.Rest.ViewModels  // used for PUT
{
  public class NodeViewModelPut : IValidatableObject
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

    [JsonPropertyName("zmqNotificationsEndpoint")]
    public string ZMQNotificationsEndpoint { get; set; }

    public Node ToDomainObject()
    {
      var (host, port) = Node.SplitHostAndPort(Id);
      return new Node(
        host,
        port,
        Username,
        Password,
        Remarks,
        ZMQNotificationsEndpoint);
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
      if (!string.IsNullOrEmpty(ZMQNotificationsEndpoint)) // null/empty string value or "tcp://a.b.c.d:port" 
      {
        if (!CommonValidator.IsUrlWithUriSchemesValid(ZMQNotificationsEndpoint, nameof(ZMQNotificationsEndpoint), new string[] { "tcp" }, out var error))
        {
          yield return new ValidationResult(error, new[] { nameof(ZMQNotificationsEndpoint) });
        }
      }
    }
  }
}
