// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.PaymentAggregator.Domain.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MerchantAPI.PaymentAggregator.Rest.ViewModels
{
  public class GatewayViewModelGet
  {
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; }

    [JsonPropertyName("minerRef")]
    public string MinerRef { get; set; }

    [JsonPropertyName("email")]
    public string Email { get; set; }

    [JsonPropertyName("organisationName")]
    public string OrganisationName { get; set; }

    [JsonPropertyName("contactFirstName")]
    public string ContactFirstName { get; set; }

    [JsonPropertyName("contactLastName")]
    public string ContactLastName { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; private set; }

    [JsonPropertyName("remarks")]
    public string Remarks { get; set; }

    [JsonPropertyName("lastError")]
    public string LastError { get; set; }

    [JsonPropertyName("lastErrorAt")]
    public DateTime? LastErrorAt { get; set; }

    [JsonPropertyName("disabledAt")]
    public DateTime? DisabledAt { get; set; }

    [JsonIgnore]
    public DateTime? DeletedAt { get; set; }

    public GatewayViewModelGet()
    { }

    public GatewayViewModelGet(Gateway domain)
    {
      Id = domain.Id;
      Url = domain.Url;
      MinerRef = domain.MinerRef;
      Email = domain.Email;
      OrganisationName = domain.OrganisationName;
      ContactFirstName = domain.ContactFirstName;
      ContactLastName = domain.ContactLastName;
      CreatedAt = domain.CreatedAt;
      Remarks = domain.Remarks;
      LastError = domain.LastError;
      LastErrorAt = domain.LastErrorAt;
      DisabledAt = domain.DisabledAt;
      DeletedAt = domain.DeletedAt;
    }

  }
}
