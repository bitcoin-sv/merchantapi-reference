// Copyright(c) 2021 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using System.Text.Json.Serialization;
using MerchantAPI.APIGateway.Domain.Models;

namespace MerchantAPI.APIGateway.Rest.ViewModels
{
  public class BlockParserStatusViewModelGet
  {
    public class BlockParserSettingsViewModelGet
    {
      [JsonPropertyName("dontParseBlocks")]
      public bool DontParseBlocks { get; set; }
      [JsonPropertyName("dontInsertTransactions")]
      public bool DontInsertTransactions { get; set; }
      [JsonPropertyName("deltaBlockHeightForDoubleSpendCheck")]
      public long DeltaBlockHeightForDoubleSpendCheck { get; set; }
      [JsonPropertyName("maxBlockChainLengthForFork")]
      public int MaxBlockChainLengthForFork { get; set; }

      public string PrepareForLogging()
      {
        return $@"Settings: {nameof(DontParseBlocks)}='{ DontParseBlocks }', {nameof(DontInsertTransactions)}='{ DontInsertTransactions }',
{nameof(DeltaBlockHeightForDoubleSpendCheck)}='{ DeltaBlockHeightForDoubleSpendCheck }', {nameof(MaxBlockChainLengthForFork)}='{ MaxBlockChainLengthForFork }'.";
      }
    }

    [JsonPropertyName("blocksProcessed")]
    public long BlocksProcessed { get; set; }
    [JsonPropertyName("blocksParsed")]
    public long BlocksParsed { get; set; }
    [JsonPropertyName("totalBytes")]
    public ulong TotalBytes { get; set; }
    [JsonPropertyName("totalTxs")]
    public ulong TotalTxs { get; set; }
    [JsonPropertyName("totalTxsFound")]
    public long TotalTxsFound { get; set; }
    [JsonPropertyName("totalDsFound")]
    public long TotalDsFound { get; set; }
    [JsonPropertyName("lastBlockHash")]
    public string LastBlockHash { get; set; }
    [JsonPropertyName("lastBlockHeight")]
    public long? LastBlockHeight { get; set; }
    [JsonPropertyName("lastBlockParsedAt")]
    public DateTime? LastBlockParsedAt { get; set; }
    [JsonPropertyName("lastBlockParseTime")]
    public TimeSpan? LastBlockParseTime { get; set; }
    [JsonPropertyName("lastBlockInQueueAndParseTime")]
    public TimeSpan? LastBlockInQueueAndParseTime { get; set; }
    [JsonPropertyName("averageParseTime")]
    public TimeSpan? AverageParseTime { get; set; }
    [JsonPropertyName("averageTxParseTime")]
    public TimeSpan? AverageTxParseTime { get; set; }
    [JsonPropertyName("averageBlockDownloadSpeed")]
    public string AverageBlockDownloadSpeed 
    { 
      get 
      {
        return $"{ (averageBlockDownloadSpeedValue ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture) } Mb/s";
      } 
    }
    readonly double? averageBlockDownloadSpeedValue;
    [JsonPropertyName("maxParseTime")]
    public TimeSpan? MaxParseTime { get; set; }
    [JsonPropertyName("numOfErrors")]
    public long NumOfErrors { get; set; }
    [JsonPropertyName("blockParserQueue")]
    public long BlockParserQueue { get; set; }
    [JsonPropertyName("blockParserDescription")]
    public string BlockParserDescription { get; set; }
    [JsonPropertyName("blockParserSettings")]
    public BlockParserSettingsViewModelGet Settings { get; set; }

    public BlockParserStatusViewModelGet(BlockParserStatus blockParserStatus, bool dontParseBlocks, bool dontInsertTransactions, long deltaBlockHeightForDoubleSpendCheck, int maxBlockChainLengthForFork)
    {
      BlocksProcessed = blockParserStatus.BlocksProcessed;
      BlocksParsed = blockParserStatus.BlocksParsed;
      TotalBytes = blockParserStatus.TotalBytes;
      TotalTxs = blockParserStatus.TotalTxs;
      TotalTxsFound = blockParserStatus.TotalTxsFound;
      TotalDsFound = blockParserStatus.TotalDsFound;
      LastBlockHash = blockParserStatus.LastBlockHash;
      LastBlockHeight = blockParserStatus.LastBlockHeight;
      LastBlockParsedAt = blockParserStatus.LastBlockParsedAt;
      LastBlockParseTime = blockParserStatus.LastBlockParseTime;
      LastBlockInQueueAndParseTime = blockParserStatus.LastBlockInQueueAndParseTime;
      AverageParseTime = blockParserStatus.AverageParseTime;
      AverageTxParseTime = blockParserStatus.AverageTxParseTime;
      averageBlockDownloadSpeedValue = blockParserStatus.AverageBlockDownloadSpeed;
      MaxParseTime = blockParserStatus.MaxParseTime;
      NumOfErrors = blockParserStatus.NumOfErrors;
      BlockParserQueue = blockParserStatus.BlocksQueued;
      BlockParserDescription = blockParserStatus.BlockParserDescription;
      Settings = new();
      Settings.DontParseBlocks = dontParseBlocks;
      Settings.DontInsertTransactions = dontInsertTransactions;
      Settings.DeltaBlockHeightForDoubleSpendCheck = deltaBlockHeightForDoubleSpendCheck;
      Settings.MaxBlockChainLengthForFork = maxBlockChainLengthForFork;
    }

    public string PrepareForLogging()
    {
      return $@"BlockParserDescription: '{ BlockParserDescription }'.
Total stats: {nameof(TotalBytes)}='{TotalBytes} bytes', {nameof(TotalTxs)}='{TotalTxs}', {nameof(TotalDsFound)}='{TotalDsFound}', {nameof(TotalTxsFound)}='{TotalTxsFound}'
Last block stats: { 
  (string.IsNullOrEmpty(LastBlockHash) ? "unknown" 
: $"{nameof(LastBlockHash)}='{LastBlockHash}', {nameof(LastBlockHeight)}='{LastBlockHeight}', {nameof(LastBlockParseTime)}='{LastBlockParseTime.Value.TotalMilliseconds} ms'.")
}
Average stats: {nameof(AverageParseTime)}='{AverageParseTime?.TotalMilliseconds ?? 0} ms', {nameof(AverageTxParseTime)}='{AverageTxParseTime?.TotalMilliseconds ?? 0} ms', {nameof(AverageBlockDownloadSpeed)}='{AverageBlockDownloadSpeed}'.
Max stats: {nameof(MaxParseTime)}='{MaxParseTime?.TotalMilliseconds ?? 0} ms'.
{ Settings.PrepareForLogging() }";
    }
  }
}
