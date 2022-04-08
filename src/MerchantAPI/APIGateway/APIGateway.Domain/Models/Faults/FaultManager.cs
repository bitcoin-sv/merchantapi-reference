// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using System.Collections.Generic;
using System.Linq;
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

    public FaultTrigger GetFaultById(string id)
    {
      lock (faultLock)
      {
        return faults.SingleOrDefault(x => IdsEqual(x.Id, id));
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

    private static bool IdsEqual(string id1, string id2)
    {
      return id1.Equals(id2, StringComparison.OrdinalIgnoreCase);
    }

    public void Update(FaultTrigger fault)
    {
      if (fault.Id != null)
      {
        var oldFault = faults.Single(x => IdsEqual(x.Id, fault.Id));
        lock (faultLock)
        {
          faults[faults.IndexOf(oldFault)] = fault;
        }
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
        faults.RemoveAll(x => IdsEqual(x.Id, id));
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
            break;
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
      switch (fault.DbFaultMethod)
      {
        case DbFaultMethod.Exception:
          throw new FaultException($"Fault { fault.Name ?? "(no name)" } id { fault.Id }");
        case DbFaultMethod.ProcessExit:
          Environment.Exit(99);
          throw new FaultException($"Exit fault { fault.Name ?? "(no name)" } id { fault.Id }");
        default:
          throw new NotImplementedException($"FaultMethod { fault.DbFaultMethod }");
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
