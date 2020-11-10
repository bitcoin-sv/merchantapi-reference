// Copyright (c) 2020 Bitcoin Association

using System;
using System.Text.Json.Serialization;

namespace MerchantAPI.PaymentAggregator.Rest.ViewModels
{
  public class SubmitTransactionResponseViewModel
  {
    public SubmitTransactionResponseViewModel() { }

    [JsonPropertyName("apiVersion")]
    public string ApiVersion { get; set; }


    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("txid")]
    public string Txid { get; set; }

    [JsonPropertyName("returnResult")]
    public string ReturnResult { get; set; }

    [JsonPropertyName("resultDescription")]
    public string ResultDescription { get; set; }

    [JsonPropertyName("minerId")]
    public string MinerId { get; set; }

    [JsonPropertyName("currentHighestBlockHash")]
    public string CurrentHighestBlockHash { get; set; }

    [JsonPropertyName("currentHighestBlockHeight")]
    public long CurrentHighestBlockHeight { get; set; }

    [JsonPropertyName("txSecondMempoolExpiry")]
    public long TxSecondMempoolExpiry { get; set; } // in minutes 

    [JsonPropertyName("conflictedWith")]
    public SubmitTransactionConflictedTxResponseViewModel[] ConflictedWith { get; set; }

  }
}
