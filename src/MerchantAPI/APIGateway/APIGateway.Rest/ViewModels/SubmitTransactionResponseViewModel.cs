// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using System.Linq;
using System.Text.Json.Serialization;
using MerchantAPI.APIGateway.Domain;
using MerchantAPI.APIGateway.Domain.Models;

namespace MerchantAPI.APIGateway.Rest.ViewModels
{
  public class SubmitTransactionResponseViewModel
  {

    public SubmitTransactionResponseViewModel()
    {
    }

    public SubmitTransactionResponseViewModel(SubmitTransactionResponse domain)
    {
      ApiVersion = Const.MERCHANT_API_VERSION;
      Timestamp = domain.Timestamp;
      Txid = domain.Txid;
      ReturnResult = domain.ReturnResult ?? ""; // return empty strings instead of nulls
      ResultDescription = domain.ResultDescription ?? "";
      MinerId = domain.MinerId ?? "";
      CurrentHighestBlockHash = domain.CurrentHighestBlockHash;
      CurrentHighestBlockHeight = domain.CurrentHighestBlockHeight;
      TxSecondMempoolExpiry = domain.TxSecondMempoolExpiry;
      Warnings = domain.Warnings;
      FailureRetryable = domain.FailureRetryable;
      ConflictedWith = domain.ConflictedWith?.Select(t => new SubmitTransactionConflictedTxResponseViewModel(t)).ToArray();
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

    [JsonPropertyName("minerId")]
    public string MinerId { get; set; }

    [JsonPropertyName("currentHighestBlockHash")]
    public string CurrentHighestBlockHash { get; set; }

    [JsonPropertyName("currentHighestBlockHeight")]
    public long CurrentHighestBlockHeight { get; set; }

    [JsonPropertyName("txSecondMempoolExpiry")]
    public long TxSecondMempoolExpiry { get; set; } // in minutes 

    [JsonPropertyName("warnings")]
    public string[] Warnings { get; set; }

    [JsonPropertyName("failureRetryable")]
    public bool FailureRetryable { get; set; }

    [JsonPropertyName("conflictedWith")]
    public SubmitTransactionConflictedTxResponseViewModel[] ConflictedWith { get; set; }

  }
}
