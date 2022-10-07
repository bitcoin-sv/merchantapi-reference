// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System.Threading.Tasks;
using static MerchantAPI.APIGateway.Domain.Faults;

namespace MerchantAPI.APIGateway.Domain.Models.Faults
{
  public interface IFaultInjection
  {
    public Task FailBeforeSavingUncommittedStateAsync(DbFaultComponent? component);
    public Task FailAfterSavingUncommittedStateAsync(DbFaultComponent? component);
    public Task<SimulateSendTxsResponse?> SimulateSendTxsResponseAsync(FaultType? faultType);
  }
}
