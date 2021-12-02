// Copyright(c) 2021 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;

namespace MerchantAPI.APIGateway.Domain.Models
{
  public class BlockParserStatus
  {
    public long BlocksProcessed { get; set; }
    public long BlocksParsed { get; set; }
    public ulong TotalBytes { get; set; }
    public ulong TotalTxs { get; set; }
    public long TotalTxsFound { get; set; }
    public long TotalDsFound { get; set; }
    public string LastBlockHash { get; set; }
    public long? LastBlockHeight { get; set; }
    public DateTime? LastBlockParsedAt { get; set; }
    public TimeSpan? LastBlockInQueueAndParseTime { get; set; }
    public TimeSpan? LastBlockParseTime { get; set; }
    public TimeSpan BlocksParseTime { get; set; }
    public TimeSpan? AverageParseTime { 
      get 
      {
        return BlocksParsed > 0 ? BlocksParseTime / BlocksParsed : null;
      } 
    }
    public TimeSpan? AverageTxParseTime 
    { 
      get
      {
        return TotalTxs > 0 ? BlocksParseTime / TotalTxs : null;
      }
    }
    public TimeSpan BlocksDownloadTime { get; set; }
    public double? AverageBlockDownloadSpeed
    {
      get
      {
        return TotalBytes > 0 ? (TotalBytes / 1000000.0) / BlocksDownloadTime.TotalSeconds : null;
      }
    }
    public TimeSpan? MaxParseTime { get; set; }
    public int NumOfErrors { get; set; }
    public int BlockParserQueue { get; set; }
    public string BlockParserDescription
    {
      get
      {
        return $"Number of blocks processed: {BlocksProcessed} " +
          $"(successfully parsed: {BlocksParsed}, " +
          $"ignored/duplicates: {BlocksProcessed - BlocksParsed}, " +
          $"parsing terminated with error {NumOfErrors}). " +
          $"Number of blocks remaining: {BlockParserQueue}.";
      }
    }

    public void IncrementBlockProcess(int blockCheckerQueue)
    {
      BlocksProcessed++;
      if (blockCheckerQueue == 0)
      {
        BlockParserQueue--;
      }
      else
      {
        BlockParserQueue += --blockCheckerQueue;
      }
    }

  }
}
