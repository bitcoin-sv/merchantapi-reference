// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;

namespace MerchantAPI.Common.Clock
{
  public class Clock: IClock
  {
    public DateTime UtcNow()
    {
       return DateTime.UtcNow; 
    }
  }
}
