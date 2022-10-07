// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using CsvHelper;
using CsvHelper.Configuration;
using System;
using System.Globalization;
using System.IO;

namespace MerchantAPI.APIGateway.Test.Stress
{
  internal class CsvUtils
  {
    public static void GenerateCsvRow(string mAPIversion, int mempoolTxs, SendConfig config, Stats stats)
    {
      StatsCsv statsCsv = new(mAPIversion, config.Filename, config.BatchSize, config.Threads, !string.IsNullOrEmpty(config.MapiConfig.Callback?.Url),
        config.GetRawMempoolEveryNTxs, mempoolTxs, stats, config.CsvComment);

      string statsFile = "stats.csv";
      bool fileExists = File.Exists(statsFile);
      using var stream = File.Open(statsFile, FileMode.Append);
      using var writer = new StreamWriter(stream);
      CsvConfiguration conf = new(CultureInfo.InvariantCulture)
      {
        Delimiter = ";",
        DetectColumnCountChanges = true
      };

      using var csv = new CsvWriter(writer, conf);
      if (!fileExists)
      {
        csv.WriteHeader<StatsCsv>();
        csv.NextRecord();
      }
      csv.WriteRecord(statsCsv);
      csv.NextRecord();
      csv.Flush();
    }

    public static void GenerateMempoolCsvRow(long txsSubmitted, int mempoolCount, TimeSpan elapsed)
    {
      string statsFile = "statsMempool.csv";
      bool fileExists = File.Exists(statsFile);
      using var stream = File.Open(statsFile, FileMode.Append);
      using var writer = new StreamWriter(stream);
      CsvConfiguration conf = new(CultureInfo.InvariantCulture)
      {
        Delimiter = ";",
        DetectColumnCountChanges = true
      };

      using var csv = new CsvWriter(writer, conf);
      if (!fileExists)
      {
        csv.WriteField("txsSubmitted");
        csv.WriteField("mempoolCount");
        csv.WriteField("elapsed");
        csv.NextRecord();
        csv.Flush();
      }
      csv.WriteField(txsSubmitted);
      csv.WriteField(mempoolCount);
      csv.WriteField(elapsed);
      csv.NextRecord();
      csv.Flush();
    }
  }
}
