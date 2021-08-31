// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using MerchantAPI.Common.Clock;

namespace MerchantAPI.Common.Test.Clock
{

  public class MockedClock : IMockedClock
  {
    private static readonly object lockObj = new();
    private static DateTime? _nowForTest;

    public static DateTime UtcNow
    {
      get
      {
        lock (lockObj)
        {
          return _nowForTest ?? DateTime.UtcNow;
        }
      }
    }

    DateTime IClock.UtcNow()
    {
      lock (lockObj)
      {
        return MockedClock.UtcNow;
      }
    }

    public static IDisposable NowIs(DateTime dateTime)
    {
      lock (lockObj)
      {
        _nowForTest = dateTime;
        return new MockedClock();
      }
    }

    IDisposable IMockedClock.NowIs(DateTime dateTime)
    {
      return MockedClock.NowIs(dateTime);
    }

    public void Dispose()
    {
      lock (lockObj)
      {
        _nowForTest = null;
      }
    }

  };
}
