// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.PaymentAggregator.Domain.Models;
using System;
using System.Text.Json.Serialization;

namespace MerchantAPI.PaymentAggregator.Rest.ViewModels
{
  public class AccountViewModelGet
  {
    public AccountViewModelGet() { }

    public AccountViewModelGet(Account account)
    {
      AccountId = account.AccountId;
      OrganisationName = account.OrganisationName;
      ContactFirstName = account.ContactFirstName;
      ContactLastName = account.ContactLastName;
      Email = account.Email;
      Identity = account.Identity;
      CreatedAt = account.CreatedAt;
      IdentityProvider = account.IdentityProvider;
    }

    [JsonPropertyName("accountId")]
    public int AccountId { get; set; }
    
    [JsonPropertyName("organisationName")]
    public string OrganisationName { get; set; }
    
    [JsonPropertyName("contactFirstName")]
    public string ContactFirstName { get; set; }
    
    [JsonPropertyName("contactLastName")]
    public string ContactLastName { get; set; }
    
    [JsonPropertyName("email")]
    public string Email { get; set; }
    
    [JsonPropertyName("identity")]
    public string Identity { get; set; }
    
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }
    
    [JsonPropertyName("identityProvider")]
    public string IdentityProvider { get; set; }
  }
}
