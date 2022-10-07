// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain.Models.Faults;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using static MerchantAPI.APIGateway.Domain.Faults;

namespace MerchantAPI.APIGateway.Rest.ViewModels.Faults
{
  public class FaultTriggerViewModelCreate : IValidatableObject
  {
    [Required]
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [Required]
    [JsonPropertyName("type")]
    public string Type { get; set; }

    private FaultType FaultType => (FaultType)Enum.Parse(typeof(FaultType), Type);

    [JsonPropertyName("dbFaultComponent")]
    public string DbFaultComponent { get; set; }

    [JsonPropertyName("faultMethod")]
    public string DbFaultMethod { get; set; } = Domain.Faults.DbFaultMethod.Exception.ToString();

    [JsonPropertyName("simulateSendTxsResponse")]
    public string SimulateSendTxsResponse { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("faultDelayMs")]
    public int? FaultDelayMs { get; set; }

    [Range(0, 100)]
    [JsonPropertyName("faultProbability")]
    public int FaultProbability { get; set; } = 100;


    public FaultTriggerViewModelCreate() { }

    public FaultTrigger ToDomainObject()
    {
      return new FaultTrigger
      {
        Id = Id,
        Type = FaultType,
        DbFaultComponent = DbFaultComponent != null ? (DbFaultComponent)Enum.Parse(typeof(DbFaultComponent), DbFaultComponent) : null,
        SimulateSendTxsResponse = SimulateSendTxsResponse != null ? (SimulateSendTxsResponse)Enum.Parse(typeof(SimulateSendTxsResponse), SimulateSendTxsResponse) : null,
        Name = Name,
        FaultDelayMs = FaultDelayMs,
        FaultProbability = FaultProbability,
        DbFaultMethod = DbFaultMethod != null ? (DbFaultMethod)Enum.Parse(typeof(DbFaultMethod), DbFaultMethod) : null,
      };
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
      if (FaultType == FaultType.DbBeforeSavingUncommittedState || FaultType == FaultType.DbAfterSavingUncommittedState)
      {
        if (DbFaultComponent == null)
        {
          yield return new ValidationResult($"{ nameof(DbFaultComponent) } must be present.");
        }
        if (SimulateSendTxsResponse != null)
        {
          yield return new ValidationResult($"{ nameof(SimulateSendTxsResponse) } must be removed.");
        }
      }
      else
      {
        if (SimulateSendTxsResponse == null)
        {
          yield return new ValidationResult($"{ nameof(SimulateSendTxsResponse) } must be present.");
        }
        if (DbFaultComponent != null)
        {
          yield return new ValidationResult($"{ nameof(DbFaultComponent) } must be removed.");
        }
      }
    }
  }
}
