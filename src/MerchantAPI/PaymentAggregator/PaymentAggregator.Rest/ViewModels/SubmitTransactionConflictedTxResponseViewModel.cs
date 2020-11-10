// Copyright (c) 2020 Bitcoin Association

using System.Text.Json.Serialization;

namespace MerchantAPI.PaymentAggregator.Rest.ViewModels
{
  public class SubmitTransactionConflictedTxResponseViewModel
  {
    public SubmitTransactionConflictedTxResponseViewModel() {}

    [JsonPropertyName("txid")]
    public string Txid { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("hex")]
    public string Hex { get; set; }
  }
}
