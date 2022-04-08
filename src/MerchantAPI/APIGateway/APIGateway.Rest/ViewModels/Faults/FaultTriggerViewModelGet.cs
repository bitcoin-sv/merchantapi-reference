// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain.Models.Faults;
using System.Text.Json.Serialization;

namespace MerchantAPI.APIGateway.Rest.ViewModels.Faults
{
  public class FaultTriggerViewModelGet
  {
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("dbFaultComponent")]
    public string DbFaultComponent { get; set; }

    [JsonPropertyName("faultMethod")]
    public string DbFaultMethod { get; set; }

    [JsonPropertyName("simulateSendTxsResponse")]
    public string SimulateSendTxsResponse { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("faultDelayMs")]
    public int? FaultDelayMs { get; set; }

    [JsonPropertyName("faultProbability")]
    public int FaultProbability { get; set; }

    public FaultTriggerViewModelGet()
    {
    }

    public FaultTriggerViewModelGet(FaultTrigger faultTrigger)
    {
      Id = faultTrigger.Id;
      Type = faultTrigger.Type.ToString();
      DbFaultComponent = faultTrigger.DbFaultComponent?.ToString();
      SimulateSendTxsResponse = faultTrigger.SimulateSendTxsResponse?.ToString();
      Name = faultTrigger.Name;
      FaultDelayMs = faultTrigger.FaultDelayMs;
      FaultProbability = faultTrigger.FaultProbability;
      DbFaultMethod = faultTrigger.DbFaultMethod?.ToString();
    }
  }
}
