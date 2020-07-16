// Copyright (c) 2020 Bitcoin Association

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
