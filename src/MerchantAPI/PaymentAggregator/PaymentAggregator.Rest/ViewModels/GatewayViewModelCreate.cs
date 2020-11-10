// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.PaymentAggregator.Domain.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MerchantAPI.PaymentAggregator.Rest.ViewModels
{
  public class GatewayViewModelCreate // used for POST and PUT
  {

    [JsonIgnore]
    public int Id { get; set; }

    [Required]
    [JsonPropertyName("url")]
    public string Url { get; set; }

    [Required]
    [JsonPropertyName("minerRef")]
    public string MinerRef { get; set; }

    [Required]
    [JsonPropertyName("email")]
    public string Email { get; set; }

    [Required]
    [JsonPropertyName("organisationName")]
    public string OrganisationName { get; set; }

    [Required]
    [JsonPropertyName("contactFirstName")]
    public string ContactFirstName { get; set; }

    [Required]
    [JsonPropertyName("contactLastName")]
    public string ContactLastName { get; set; }

    [JsonIgnore]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("remarks")]
    public string Remarks { get; set; }

    [JsonPropertyName("disabledAt")]
    public DateTime? DisabledAt { get; set; }

    public Gateway ToDomainObject(DateTime? utcNow = null)
    {
      return new Gateway(
         Id,
         Url,
         MinerRef,
         Email,
         OrganisationName,
         ContactFirstName,
         ContactLastName,
         Remarks,
         utcNow ?? CreatedAt,
         DisabledAt
        );
    }

  }
}
