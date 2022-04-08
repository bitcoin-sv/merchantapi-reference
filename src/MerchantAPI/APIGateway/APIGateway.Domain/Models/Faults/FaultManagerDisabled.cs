// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static MerchantAPI.APIGateway.Domain.Faults;

namespace MerchantAPI.APIGateway.Domain.Models.Faults
{
  public class FaultManagerDisabled : IFaultManager, IFaultInjection
  {
    private const string Message = "fault injection disabled";
    public List<FaultTrigger> GetList()
    {
      throw new Exception(Message);
    }

    public FaultTrigger GetFaultById(string id)
    {
      throw new Exception(Message);
    }

    public void Add(FaultTrigger fault)
    {
      throw new Exception(Message);
    }

    public void Update(FaultTrigger fault)
    {
      throw new Exception(Message);
    }

    public void Clear()
    {
      throw new Exception(Message);
    }

    public void Remove(string id)
    {
      throw new Exception(Message);
    }

    public Task FailBeforeSavingUncommittedStateAsync(DbFaultComponent? component)
    {
      return Task.CompletedTask;
    }

    public Task FailAfterSavingUncommittedStateAsync(DbFaultComponent? component)
    {
      return Task.CompletedTask;
    }

    public Task<SimulateSendTxsResponse?> SimulateSendTxsResponseAsync(FaultType? faultType)
    {
      return Task.FromResult((SimulateSendTxsResponse?)null);
    }
  }
}
