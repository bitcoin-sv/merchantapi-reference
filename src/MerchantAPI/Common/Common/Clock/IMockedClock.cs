// Copyright (c) 2020 Bitcoin Association

using System;

namespace MerchantAPI.Common.Clock
{
  public interface IMockedClock : IClock, IDisposable
  {
    /// <summary>
    ///  Set the clock to specified value until disposed
    /// </summary>
    public IDisposable NowIs(DateTime dateTime);

  }
}
