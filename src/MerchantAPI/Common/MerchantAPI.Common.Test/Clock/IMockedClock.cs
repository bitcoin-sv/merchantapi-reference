// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.Common.Clock;
using System;

namespace MerchantAPI.Common.Test.Clock
{
  public interface IMockedClock : IClock, IDisposable
  {
    /// <summary>
    ///  Set the clock to specified value until disposed
    /// </summary>
    public IDisposable NowIs(DateTime dateTime);

  }
}
