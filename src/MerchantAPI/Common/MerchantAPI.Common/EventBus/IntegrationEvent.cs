// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.Common.Clock;
using System;

namespace MerchantAPI.Common.EventBus
{
  public class IntegrationEvent
  {
    public IntegrationEvent()
    {
      Id = Guid.NewGuid();
      CreationDate = DateTime.UtcNow;
    }

    public IntegrationEvent(Guid id, DateTime createDate)
    {
      Id = id;
      CreationDate = createDate;
    }

    public Guid Id { get; private set; }

    public DateTime CreationDate { get; set; }
  }
}
