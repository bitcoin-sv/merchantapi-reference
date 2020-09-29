// Copyright (c) 2020 Bitcoin Association

using System;
using System.Diagnostics;
using System.Threading;

namespace MerchantAPI.APIGateway.Test.Stress
{

  /// <summary>
  /// Tracks transaction submission statistics. Thread safe.
  /// </summary>
  public class Stats
  {
    Stopwatch sw = new Stopwatch();

    long requestErrors;
    long requestTxFailures;
    long okSubmitted;

    long callbacksReceived;

    object lockObj = new object();
    DateTime lastUpDateTimeUtc =DateTime.UtcNow;
    
    void  UpdateLastUpdateTime()
    {
      lock (lockObj)
      {
        lastUpDateTimeUtc = DateTime.UtcNow;
      }
    }

    public Stats()
    {
      sw.Start();
    }

    public void StopTiming()
    {
      sw.Stop();
    }

    public void IncrementRequestErrors()
    {
      Interlocked.Increment(ref requestErrors);
      UpdateLastUpdateTime();
    }

    public void IncrementCallbackReceived()
    {
      Interlocked.Increment(ref callbacksReceived);
      UpdateLastUpdateTime();
    }

    public void AddRequestTxFailures(int value)
    {
      Interlocked.Add(ref requestTxFailures, value);
      UpdateLastUpdateTime();
    }

    public void AddOkSubmited(int value)
    {
      Interlocked.Add(ref okSubmitted, value);
      UpdateLastUpdateTime();
    }


    public long RequestErrors => Interlocked.Read(ref requestErrors);
    public long RequestTxFailures => Interlocked.Read(ref requestTxFailures);
    public long OKSubmitted => Interlocked.Read(ref okSubmitted);

    public long CallBacksReceived => Interlocked.Read(ref callbacksReceived);


    public int LastUpdateAgeMs
    {
      get
      {
        lock (lockObj)
        {
          return (int) (DateTime.UtcNow - lastUpDateTimeUtc).TotalMilliseconds;
        }

      }
      
    }
    public override string ToString()
    {
      var elapsed = Math.Max(1, sw.ElapsedMilliseconds);

      long throughput = 1000 * (OKSubmitted + RequestTxFailures) / elapsed;
      return $"OkSubmitted: {OKSubmitted}  RequestErrors: {RequestErrors} TxFailures:{RequestTxFailures}, Throughput: {throughput} Callbacks: {CallBacksReceived}";
    }

  }
}
