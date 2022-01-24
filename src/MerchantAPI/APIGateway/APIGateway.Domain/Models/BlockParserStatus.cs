// Copyright(c) 2021 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;

namespace MerchantAPI.APIGateway.Domain.Models
{
  public class BlockParserStatus
  {
    public long BlocksProcessed 
    { 
      get 
      {
        return BlocksParsed + BlocksDuplicated + NumOfErrors;
      }
    }
    public long BlocksParsed { get; private set; }
    public long BlocksDuplicated { get; private set; }
    public ulong TotalBytes { get; private set; }
    public ulong TotalTxs { get; private set; }
    public long TotalTxsFound { get; private set; }
    public long TotalDsFound { get; private set; }
    public string LastBlockHash { get; private set; }
    public long? LastBlockHeight { get; private set; }
    public DateTime? LastBlockParsedAt { get; private set; }
    public TimeSpan? LastBlockInQueueAndParseTime { get; private set; }
    public TimeSpan? LastBlockParseTime { get; private set; }
    public TimeSpan BlocksParseTime { get; private set; }
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
    public TimeSpan BlocksDownloadTime { get; private set; }
    public double? AverageBlockDownloadSpeed
    {
      get
      {
        return TotalBytes > 0 ? (TotalBytes / Const.Megabyte) / BlocksDownloadTime.TotalSeconds : null;
      }
    }
    public TimeSpan? MaxParseTime { get; private set; }
    public long NumOfErrors { get; private set; }
    public long BlocksQueued { get; private set; }
    public string BlockParserDescription
    {
      get
      {
        return $@"Number of blocks successfully parsed: { BlocksParsed }, ignored/duplicates: { BlocksDuplicated }, parsing terminated with error: { NumOfErrors }. 
Number of blocks processed from queue is { BlocksProcessed }, remaining: { BlocksQueued }.";
      }
    }

    public void IncrementBlocksDuplicated(long queueCount)
    {
      BlocksDuplicated++;
      SetBlocksQueued(queueCount);
    }

    public void IncrementBlocksProcessed(
      long queueCount,
      string blockhash,
      long? blockHeight,
      int txsFound,
      int dsFound,
      ulong bytes,
      int txsCount,
      DateTime blockParsedAt,
      DateTime blockQueued,
      TimeSpan blockParseTime,
      TimeSpan blockDownloadTime)
    {
      SetBlocksQueued(queueCount);
      BlocksParsed++;
      LastBlockHash = blockhash;
      LastBlockHeight = blockHeight;
      TotalTxsFound += txsFound;
      TotalDsFound += dsFound;
      TotalBytes += bytes;
      TotalTxs += (ulong)txsCount;
      LastBlockParsedAt = blockParsedAt;
      LastBlockInQueueAndParseTime = blockParsedAt - blockQueued;
      LastBlockParseTime = blockParseTime;
      BlocksParseTime += blockParseTime;
      BlocksDownloadTime += blockDownloadTime;
      if (MaxParseTime == null || LastBlockParseTime > MaxParseTime)
      {
        MaxParseTime = LastBlockParseTime;
      }
    }

    public void IncrementNumOfErrors(long queueCount)
    {
      NumOfErrors++;
      SetBlocksQueued(queueCount);
    }

    public void SetBlocksQueued(long queueCount)
    {
      BlocksQueued = queueCount;
    }
  }
}
