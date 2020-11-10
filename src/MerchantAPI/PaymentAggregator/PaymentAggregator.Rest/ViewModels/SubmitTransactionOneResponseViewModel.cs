// Copyright (c) 2020 Bitcoin Association

using System.Text.Json.Serialization;

namespace MerchantAPI.PaymentAggregator.Rest.ViewModels
{
  public class SubmitTransactionOneResponseViewModel
  {
    public SubmitTransactionOneResponseViewModel()
    {
    }

    [JsonPropertyName("txid")]
    public string Txid { get; set; }

    [JsonPropertyName("returnResult")]
    public string ReturnResult { get; set; }

    [JsonPropertyName("resultDescription")]
    public string ResultDescription { get; set; }

    [JsonPropertyName("conflictedWith")]
    public SubmitTransactionConflictedTxResponseViewModel[] ConflictedWith { get; set; }
  }
}
