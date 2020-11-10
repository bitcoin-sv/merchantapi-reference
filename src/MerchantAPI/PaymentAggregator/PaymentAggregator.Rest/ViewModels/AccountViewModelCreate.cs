// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.PaymentAggregator.Domain.Models;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace MerchantAPI.PaymentAggregator.Rest.ViewModels
{
  public class AccountViewModelCreate 
  {
    [JsonIgnore]
    public int Id { get; set; }

    [JsonPropertyName("organisationName")]
    [Required]
    public string OrganisationName { get; set; }

    [JsonPropertyName("contactFirstName")]
    public string ContactFirstName { get; set; }

    [JsonPropertyName("contactLastName")]
    public string ContactLastName { get; set; }

    [JsonPropertyName("email")]
    [Required]
    public string Email { get; set; }

    [JsonPropertyName("identity")]
    [Required]
    public string Identity { get; set; }

    [JsonPropertyName("identityProvider")]
    [Required]
    public string IdentityProvider { get; set; } 

    public Account ToDomainModel()
    {
      return new Account
      {
        AccountId = Id,
        ContactFirstName = ContactFirstName,
        ContactLastName = ContactLastName,
        Email = Email,
        Identity = Identity,
        IdentityProvider = IdentityProvider,
        OrganisationName = OrganisationName,
      };
    }
  }
}
