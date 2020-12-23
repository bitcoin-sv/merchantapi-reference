// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.APIGateway.Domain.Models;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MerchantAPI.APIGateway.Rest.ViewModels
{
  public class NodeViewModelCreate : IValidatableObject // used for POST
  {

    [JsonPropertyName("id")]
    [Required]
    public string Id { get; set; } // Host + port

    [JsonPropertyName("username")]
    [Required]
    public string Username { get; set; }

    [JsonPropertyName("password")]
    [Required]
    public string Password { get; set; }

    [JsonPropertyName("remarks")]
    public string Remarks { get; set; }

    [JsonPropertyName("ZMQNotificationsEndpoint")]
    public string ZMQNotificationsEndpoint { get; set; }

    // No NodeStatus

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
      var split = Id.Split(':');

      if (split.Length != 2)
      {
        yield return new ValidationResult(
          $"The {nameof(Id)} field must be separated by exactly one ':'",
            new[] { nameof(Id) });
      }
      else
      {
        if (!int.TryParse(split[1], out var _))
        {
          yield return new ValidationResult(
            $"The {nameof(Id)} field must have number after ':'",
              new[] { nameof(Id) });
        }
      }
    }
  }
}
