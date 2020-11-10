// Copyright (c) 2020 Bitcoin Association

using System;
using System.Text.Json.Serialization;

namespace MerchantAPI.PaymentAggregator.Rest.ViewModels
{
  public class SubmitTransactionsResponseViewModel
  {
    [JsonPropertyName("apiVersion")]
    public string ApiVersion { get; set; }


    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("minerId")]
    public string MinerId { get; set; }

    [JsonPropertyName("currentHighestBlockHash")]
    public string CurrentHighestBlockHash { get; set; }

    [JsonPropertyName("currentHighestBlockHeight")]
    public long CurrentHighestBlockHeight { get; set; }

    [JsonPropertyName("txSecondMempoolExpiry")]
    public long TxSecondMempoolExpiry { get; set; }

    [JsonPropertyName("txs")]
    public SubmitTransactionOneResponseViewModel[] Txs { get; set; }

    [JsonPropertyName("failureCount")]
    public long FailureCount { get; set; }

    public SubmitTransactionsResponseViewModel()
    {
    }
  }
}
