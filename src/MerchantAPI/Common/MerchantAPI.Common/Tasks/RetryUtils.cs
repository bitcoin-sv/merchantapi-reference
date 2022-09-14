// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.Common.Exceptions;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MerchantAPI.Common.Tasks
{
  public static class RetryUtils
  {
    public static void Exec(Action action, int retry = 6, int retryDelayMs = 100)
    {
      int initialRetry = retry;
      do
      {
        try
        {
          retry--;
          action();
          return;

        }
        catch (Exception ex)
        {
          if (retry == 0)
          {
            throw new Exception($"Failed after {initialRetry} retries", ex);
          }
        }

        Thread.Sleep(retryDelayMs);
        retryDelayMs *= 2;

      } while (retry > 0);

    }

    public static async Task ExecAsync(Func<Task> methodToExecute, int retry = 6, int sleepTimeBetweenRetries = 100, string errorMessage = "")
    {
      int initialRetry = retry;
      do
      {
        try
        {
          retry--;
          await methodToExecute();
          return;
        }
        catch (Exception ex)
        {
          if (ex is TaskCanceledException)
          {
            throw new RetryException((initialRetry - retry), ex);
          }
          if (retry == 0)
          {
            if (!string.IsNullOrEmpty(errorMessage))
            {
              throw new RetryException(initialRetry, errorMessage, ex);
            }
            throw new RetryException(initialRetry, ex);
          }
        }
        Thread.Sleep(sleepTimeBetweenRetries);
      }
      while (retry > 0);

      throw new Exception("Exec with retry reached the end");
    }
  }
}
