// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using System.Collections.Generic;
using System.Linq;

namespace MerchantAPI.APIGateway.Domain.NotificationsHandler
{
  public class HostExecutionTimes
  {
    readonly int maxNoOfSavedExecutionTimes;
    readonly int slowHttpClientThresholdInMs;
      
    // Key represents host name
    readonly Dictionary<string, (bool SlowHost, Queue<long> Times)> hostExecutionTimes = new(StringComparer.InvariantCultureIgnoreCase);

    public HostExecutionTimes(int maxNoOfSavedExecutionTimes, int slowHttpClientThresholdInMs)
    {
      this.maxNoOfSavedExecutionTimes = maxNoOfSavedExecutionTimes;
      this.slowHttpClientThresholdInMs = slowHttpClientThresholdInMs;
    }

    /// <summary>
    /// Store `maxSavedExecutionTimes` of HTTP call times so we can later determine if the host will
    /// be processed by slow or fast queue processor
    /// </summary>
    public void AddExecutionTime(string host, long executionTime)
    {
      lock (hostExecutionTimes)
      {
        if (hostExecutionTimes.TryGetValue(host, out var executionTimeData))
        {
          var executionTimeQueue = executionTimeData.Times;
          executionTimeQueue.Enqueue(executionTime);

          // Ensure there is no more than `maxSavedExecutionTimes` times in the queue
          if (executionTimeQueue.Count > maxNoOfSavedExecutionTimes)
          {
            executionTimeQueue.Dequeue();
          }
          bool slowHost = (executionTimeQueue.Sum() / executionTimeQueue.Count) > slowHttpClientThresholdInMs;
          hostExecutionTimes[host] = (slowHost, executionTimeQueue);
        }
        else
        {
          var executionTimeQueue = new Queue<long>();
          executionTimeQueue.Enqueue(executionTime);
          hostExecutionTimes.Add(host, (executionTime > slowHttpClientThresholdInMs, executionTimeQueue));
        }
      }
    }

    /// <summary>
    /// Return a list of host names that are either fast/slow
    /// </summary>
    public string[] GetHosts(bool slowHostRequested)
    {
      lock (hostExecutionTimes)
      {
        return hostExecutionTimes.Where(x => x.Value.SlowHost == slowHostRequested).Select(x => x.Key).ToArray();
      }
    }

    /// <summary>
    /// Return information if requested host is fast or slow
    /// </summary>
    public bool IsSlowHost(string hostName)
    {
      lock (hostExecutionTimes)
      {
        hostExecutionTimes.TryGetValue(hostName, out var data);
        return data.SlowHost;
      }
    }
  }
}
