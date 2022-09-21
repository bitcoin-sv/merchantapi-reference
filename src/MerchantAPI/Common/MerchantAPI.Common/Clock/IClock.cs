// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;

namespace MerchantAPI.Common.Clock
{
  public interface IClock
  {
    public DateTime UtcNow();

  }
}
