// Copyright (c) 2020 Bitcoin Association

using System;

namespace MerchantAPI.Common.Clock
{
  public interface IClock
  {
    public DateTime UtcNow();

  }
}
