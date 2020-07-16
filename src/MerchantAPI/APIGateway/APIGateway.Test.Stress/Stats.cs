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

    public Stats()
    {
      sw.Start();
    }

    public void IncrementRequestErrors()
    {
      Interlocked.Increment(ref requestErrors);
    }

    public void AddRequestTxFailures(int value)
    {
      Interlocked.Add(ref requestTxFailures, value);
    }

    public void AddOkSubmited(int value)
    {
      Interlocked.Add(ref okSubmitted, value);
    }


    public long RequestErrors => Interlocked.Read(ref requestErrors);
    public long RequestTxFailures => Interlocked.Read(ref requestTxFailures);
    public long OKSubmitted => Interlocked.Read(ref okSubmitted);

    public override string ToString()
    {
      var elapsed = Math.Max(1, sw.ElapsedMilliseconds);

      long throughput = 1000 * (OKSubmitted + RequestTxFailures) / elapsed;
      return $"OkSubmitted: {OKSubmitted}  RequestErrors: {RequestErrors} TxFailures:{RequestTxFailures}, Throughput: {throughput}";
    }

  }
}
