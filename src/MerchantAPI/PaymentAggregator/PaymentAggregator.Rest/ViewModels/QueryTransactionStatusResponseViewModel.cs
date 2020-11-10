// Copyright (c) 2020 Bitcoin Association

using System;
using System.Text.Json.Serialization;

namespace MerchantAPI.PaymentAggregator.Rest.ViewModels
{
  public class QueryTransactionStatusResponseViewModel
  {
    public QueryTransactionStatusResponseViewModel()
    {
    }

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

    [JsonPropertyName("blockHash")]
    public string BlockHash { get; set; }

    [JsonPropertyName("blockHeight")]
    public long? BlockHeight { get; set; }

    [JsonPropertyName("confirmations")]
    public long? Confirmations { get; set; }

    [JsonPropertyName("minerId")]
    public string MinerId { get; set; }

    [JsonPropertyName("txSecondMempoolExpiry")]
    public int TxSecondMempoolExpiry { get; set; }
  }
}
