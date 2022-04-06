// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain.Models.Faults;
using System.Text.Json.Serialization;
using static MerchantAPI.APIGateway.Domain.Faults;

namespace MerchantAPI.APIGateway.Rest.ViewModels.Faults
{
  public class FaultTriggerViewModelGet
  {
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("type")]
    public FaultType Type { get; set; }

    [JsonPropertyName("dbFaultComponent")]
    public DbFaultComponent DbFaultComponent { get; set; }

    [JsonPropertyName("simulateSendTxsResponse")]
    public SimulateSendTxsResponse SimulateSendTxsResponse { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("faultDelayMs")]
    public int? FaultDelayMs { get; set; }

    [JsonPropertyName("faultProbability")]
    public int FaultProbability { get; set; } = 100;

    [JsonPropertyName("faultMethod")]
    public DbFaultMethod? FaultMethod { get; set; } = Domain.Faults.DbFaultMethod.Exception;

    public FaultTriggerViewModelGet(FaultTrigger faultTrigger)
    {
      Id = faultTrigger.Id;
      Type = faultTrigger.Type;
      DbFaultComponent = faultTrigger.DbFaultComponent;
      SimulateSendTxsResponse = faultTrigger.SimulateSendTxsResponse;
      Name = faultTrigger.Name;
      FaultDelayMs = faultTrigger.FaultDelayMs;
      FaultProbability = faultTrigger.FaultProbability;
      FaultMethod = faultTrigger.FaultMethod;
    }
  }
}
