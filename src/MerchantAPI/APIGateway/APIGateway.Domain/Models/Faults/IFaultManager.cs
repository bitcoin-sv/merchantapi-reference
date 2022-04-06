// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System.Collections.Generic;

namespace MerchantAPI.APIGateway.Domain.Models.Faults
{
  public interface IFaultManager
  {
    public List<FaultTrigger> GetList();
    public void Add(FaultTrigger fault);
    public void Clear();
    void Remove(string id);
  }
}
