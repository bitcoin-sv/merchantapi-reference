// Copyright(c) 2021 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using CsvHelper.Configuration.Attributes;

namespace MerchantAPI.APIGateway.Test.Stress
{
  public class StatsCsv
  {
    [Name("mAPIVersion")]
    public string MerchantApiVersion { get; set; }
    public string Filename { get; set; }
    public int BatchSize { get; set; }
    public int Threads { get; set; }
    public bool DoCallbacks { get; set; }
    public long OKSubmitted { get; set; }
    public long RequestErrors { get; set; }
    public long RequestTxFailures { get; set; }
    public long Throughput { get; set; }
    public long CallbacksReceived { get; set; }
    public long SimulatedCallbackErrors { get; set; }
    public TimeSpan ElapsedTime { get; set; }
    public long GenerateBlockCalls { get; set; }
    public string Comment { get; set; }


    public StatsCsv(string mAPIVersion, string filename, int batchSize, int threads, bool doCallbacks, Stats stats, string comment = null)
    {
      MerchantApiVersion = mAPIVersion;
      Filename = filename;
      BatchSize = batchSize;
      Threads = threads;
      DoCallbacks = doCallbacks;
      OKSubmitted = stats.OKSubmitted;
      RequestErrors = stats.RequestErrors;
      RequestTxFailures = stats.RequestTxFailures;
      Throughput = stats.Throughput;
      CallbacksReceived = stats.CallbacksReceived;
      SimulatedCallbackErrors = stats.SimulatedCallbackErrors;
      ElapsedTime = stats.ElapsedTime;
      GenerateBlockCalls = stats.GenerateBlockCalls;
      Comment = comment;
    }

  }
}
