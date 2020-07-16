// Copyright (c) 2020 Bitcoin Association

using System;
using System.Threading;

namespace MerchantAPI.APIGateway.Test.Functional
{
  public class Utils
  {
    public static void WaitUntil(Func<bool> predicate)
    {
      for (int i = 0; i < 100; i++)
      {
        if (predicate())
        {
          return;
        }

        Thread.Sleep(100);  // see also BackgroundJobsMock.WaitForPropagation()
      }

      throw new Exception("Timeout - WaitUntil did not complete in allocated time");
    }
  }
}
