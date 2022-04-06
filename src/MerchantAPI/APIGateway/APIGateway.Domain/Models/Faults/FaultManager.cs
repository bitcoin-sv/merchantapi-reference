// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static MerchantAPI.APIGateway.Domain.Faults;

namespace MerchantAPI.APIGateway.Domain.Models.Faults
{
  public class FaultManager : IFaultManager, IFaultInjection
  {
    readonly object faultLock = new();
    readonly List<FaultTrigger> faults = new();

    public List<FaultTrigger> GetList()
    {
      lock (faultLock)
      {
        return new List<FaultTrigger>(faults);
      }
    }

    public void Add(FaultTrigger fault)
    {
      if (fault.Id == null)
      {
        fault.Id = Guid.NewGuid().ToString("N");
      }

      lock (faultLock)
      {
        faults.Add(fault);
      }
    }

    public void Clear()
    {
      lock (faultLock)
      {
        faults.Clear();
      }

    }
    public void Remove(string id)
    {
      lock (faultLock)
      {
        faults.RemoveAll(x => x.Id == id);
      }
    }

    public Task FailBeforeSavingUncommittedStateAsync(DbFaultComponent? component)
    {
      return CheckRules(FaultType.DbBeforeSavingUncommittedState, component);
    }

    public Task FailAfterSavingUncommittedStateAsync(DbFaultComponent? component)
    {
      return CheckRules(FaultType.DbAfterSavingUncommittedState, component);
    }

    public async Task<SimulateSendTxsResponse?> SimulateSendTxsResponseAsync(FaultType? faultType)
    {
      if (faultType == null)
      {
        return null;
      }
      return await CheckRulesSimulateSendTxs(faultType);
    }

    private async Task CheckRules(FaultType faultType, DbFaultComponent? dbComponent)
    {
      if (dbComponent == null)
      {
        return;
      }
      FaultTrigger fault = null;
      lock (faultLock)
      {
        Random rand = new();
        foreach (var rule in faults)
        {
          if (rule.Type != faultType ||  rule.DbFaultComponent != dbComponent)
          {
            continue;
          }

          int chance = rand.Next(1, 101);

          if (chance <= rule.FaultProbability)
          {
            fault = rule;
          }
        }
      }

      if (fault == null)
      {
        return;
      }
      if (fault.FaultDelayMs != null)
      {
        await Task.Delay(fault.FaultDelayMs.Value);
      }
      switch (fault.FaultMethod)
      {
        case DbFaultMethod.Exception:
          throw new FaultException("fault " + fault.Name + " id " + fault.Id);
        case DbFaultMethod.ProcessExit:
          Environment.Exit(99);
          throw new FaultException("exit fault " + fault.Name + " id " + fault.Id);
        default:
          throw new NotImplementedException("faultMethod " + fault.FaultMethod);
      }
    }

    private async Task<SimulateSendTxsResponse?> CheckRulesSimulateSendTxs(FaultType? faultType)
    {
      FaultTrigger fault = null;
      Random rand = new();
      lock (faultLock)
      {
        foreach (var rule in faults)
        {
          if (rule.Type != faultType)
          {
            continue;
          }

          int chance = rand.Next(1, 101);

          if (chance <= rule.FaultProbability)
          {
            fault = rule;
          }
        }
      }

      if (fault == null)
      {
        return null;
      }
      if (fault.FaultDelayMs != null)
      {
        await Task.Delay(fault.FaultDelayMs.Value);
      }
      return fault.SimulateSendTxsResponse;
    }
  }
}
