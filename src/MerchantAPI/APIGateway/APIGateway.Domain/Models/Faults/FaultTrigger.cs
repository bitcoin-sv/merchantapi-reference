// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using static MerchantAPI.APIGateway.Domain.Faults;

namespace MerchantAPI.APIGateway.Domain.Models.Faults
{
  public class FaultTrigger
  {
    public string Id { get; set; }

    public FaultType Type { get; set; }

    public DbFaultComponent? DbFaultComponent { get; set; }

    public SimulateSendTxsResponse? SimulateSendTxsResponse { get; set; }

    public string Name { get; set; }

    public int? FaultDelayMs { get; set; }

    public int FaultProbability { get; set; } = 100;

    public DbFaultMethod? DbFaultMethod { get; set; } = Domain.Faults.DbFaultMethod.Exception;
  }
}
