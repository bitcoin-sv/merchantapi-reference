// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using MerchantAPI.Common;

namespace MerchantAPI.PaymentAggregator.Domain.Models
{
  public class Gateway : IValidatableObject
  {
    public int Id { get; set; }
    public string Url { get; set; }
    public string MinerRef { get; set; }
    public string Email { get; set; }
    public string OrganisationName { get; set; }
    public string ContactFirstName { get; set; }
    public string ContactLastName { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Remarks { get; set; }
    public string LastError { get; private set; }
    public DateTime? LastErrorAt { get; private set; }
    public DateTime? DisabledAt { get; private set; }
    public DateTime? DeletedAt { get; private set; }


    public Gateway(int gatewayId, string url, string minerRef, string email, string organisationName, string contactFirstName,
               string contactLastName, string remarks, DateTime createdAt, DateTime? disabledAt)
    : this(gatewayId, url, minerRef, email, organisationName, contactFirstName, contactLastName, createdAt, remarks, null, null, disabledAt, null)
    {
    }

    public Gateway(int gatewayId, string url, string minerRef, string email, string organisationName, string contactFirstName,
                   string contactLastName, DateTime createdAt, string remarks, String lastError, 
                   DateTime? lastErrorAt, DateTime? disabledAt, DateTime? deletedAt)
    {
      Id = gatewayId;
      Url = url;
      MinerRef = minerRef;
      Email = email;
      OrganisationName = organisationName;
      ContactFirstName = contactFirstName;
      ContactLastName = contactLastName;
      CreatedAt = createdAt;
      Remarks = remarks;
      LastError = lastError;
      LastErrorAt = lastErrorAt;
      DisabledAt = disabledAt;
      DeletedAt = deletedAt;
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
      if (!CommonValidator.IsUrlValid(Url, nameof(Url), out var error))
      {
        yield return new ValidationResult(error);
      }
      else if (!Url.EndsWith("/"))
      {
        yield return new ValidationResult($"{nameof(Url)}: { Url } must end with '/'.");
      }
      if (!CommonValidator.IsEmailValid(Email))
      {
        yield return new ValidationResult($"{nameof(Email)}: { Email } is not valid.");
      }
    }

  }
}
