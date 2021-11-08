// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using NBitcoin;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace MerchantAPI.APIGateway.Test.Stress
{

  class TxByHost
  {
    /// <summary>
    /// Key contains host name. Value contains txId and number of times we have seenthis tx.
    /// </summary>
    readonly Dictionary<string, Dictionary<uint256, long>> countByHost = new();

    // total count
    long count;

    public long Count
    {

      get
      {
        lock (countByHost)
        {
          return count;
        }
      }
    }

    public void Add(string host, IEnumerable<uint256> txids)
    {

      lock (countByHost)
      {
        if (!countByHost.TryGetValue(host, out var txData))
        {
          txData = new Dictionary<uint256, long>();
          countByHost.Add(host, txData);
        }

        foreach (var txId in txids)
        {
          if (!txData.TryAdd(txId, 1))
          {
            txData[txId]++;
          }
          count++;
        }
      }
    }


    /// <summary>
    /// Calculates differences between two dictionaries
    /// </summary>
    /// <param name="first"></param>
    /// <param name="second"></param>
    /// <param name="valueDiff">Function(v1,v2) that calculates v1 -v2 </param>
    /// <param name="valueNegate">Function(v)  that calculates -v</param>
    /// <returns></returns>
    static Dictionary<K, V> DictionaryDifference<K, V>(Dictionary<K, V> first, Dictionary<K, V> second, Func<V, V, V> valueDiff, Func<V, V> valueNegate)
    {
      var result = new Dictionary<K, V>();
      foreach (var f in first)
      {
        result.Add(f.Key, second.TryGetValue(f.Key, out var otherValue) ? valueDiff(f.Value, otherValue) : f.Value);
      }

      foreach (var s in second)
      {
        if (!first.ContainsKey(s.Key))
        {
          result.Add(s.Key, valueNegate(s.Value));
        }
      }

      return result;
    }

    /// <summary>
    /// Returns difference between this and other (this-other)
    /// </summary>
    /// <returns></returns>
    public (string host, Dictionary<uint256, long> txs)[] GetDifference(TxByHost other)
    {
      var otherCopy = other.GetCopy();
      var thisCopy = this.GetCopy();


      var result = DictionaryDifference(thisCopy, otherCopy,
        (f, s) => DictionaryDifference(f, s, (v1, v2) => v1 - v2, v1 => -v1),
        (f) => f.ToDictionary(x => x.Key, x => -x.Value)
      );

      // Convert to sorted list of tuples
      return
        result.OrderBy(x => x.Key)
        .Select(x => (x.Key, x.Value))
        .ToArray();
    }

    /// <summary>
    /// Return *copy* of existing data that can be safely modified even without locks
    /// Note: uint256 is copied by reference
    /// </summary>
    /// <returns></returns>
    public Dictionary<string, Dictionary<uint256, long>> GetCopy()
    {
      lock (countByHost)
      {
        var ret = new Dictionary<string, Dictionary<uint256, long>>();
        foreach (var item in countByHost)
        {
          ret.Add(item.Key, new Dictionary<uint256, long>(item.Value));
        }

        return ret;
      }
    }
  }
  /// <summary>
  /// Tracks transaction submission statistics. Thread safe.
  /// </summary>
  public class Stats
  {
    readonly Stopwatch sw = new();

    /// <summary>
    /// Number of submitTransactions request that failed
    /// We do not track txIds for this one - we just use TxByHost time and use uint256.zero for all entries
    /// </summary>
    long requestErrors;

    long simulatedCallbackErrors;

    readonly TxByHost requestTxFailures = new();

    readonly TxByHost okSubmitted = new();

    readonly TxByHost callbackReceived = new();

    readonly object lockObj = new();
    DateTime lastUpDateTimeUtc = DateTime.UtcNow;

    void UpdateLastUpdateTime()
    {
      lock (lockObj)
      {
        lastUpDateTimeUtc = DateTime.UtcNow;
      }
    }


    public (string host, uint256[] txs)[] GetMissingCallbacksByHost()

      => callbackReceived.GetDifference(okSubmitted)
        .Select(x
          => (x.host,
              txs: x.txs.Where(t => t.Value < 0).Select(t => t.Key).ToArray()))
        .Where(x => x.txs.Any())
        .ToArray();


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

    public void IncrementSimulatedCallbackErrors()
    {
      Interlocked.Increment(ref simulatedCallbackErrors);
      UpdateLastUpdateTime();
    }
    //

    public void IncrementCallbackReceived(string host, uint256 txId)
    {
      callbackReceived.Add(host, new[] { txId });
      UpdateLastUpdateTime();
    }

    public void AddRequestTxFailures(string host, IEnumerable<uint256> txIds)
    {
      requestTxFailures.Add(host, txIds);
      UpdateLastUpdateTime();
    }

    public void AddOkSubmited(string host, IEnumerable<uint256> txIds)
    {
      okSubmitted.Add(host, txIds);
      UpdateLastUpdateTime();
    }


    public long RequestErrors => Interlocked.Read(ref requestErrors);

    public long SimulatedCallbackErrors => Interlocked.Read(ref simulatedCallbackErrors);
    public long RequestTxFailures => requestTxFailures.Count;
    public long OKSubmitted => okSubmitted.Count;
    public long CallbacksReceived => callbackReceived.Count;

    private long Elapsed
    {
      get
      {
        return Math.Max(1, sw.ElapsedMilliseconds);
      }
    }

    public long Throughput
    {
      get
      {
        long throughput = 1000 * (OKSubmitted + RequestTxFailures) / Elapsed;
        return throughput;
      }
    }

    public TimeSpan ElapsedTime
    {
      get
      {
        return TimeSpan.FromMilliseconds(Elapsed);
      }
    }


    public int LastUpdateAgeMs
    {
      get
      {
        lock (lockObj)
        {
          return (int)(DateTime.UtcNow - lastUpDateTimeUtc).TotalMilliseconds;
        }
      }
    }

    public override string ToString()
    {
      return $"OkSubmitted: {OKSubmitted}  RequestErrors: {RequestErrors} TxFailures:{RequestTxFailures}, Throughput: {Throughput} Callbacks: {CallbacksReceived} SimulatedErrors: {SimulatedCallbackErrors}";
    }

  }
}