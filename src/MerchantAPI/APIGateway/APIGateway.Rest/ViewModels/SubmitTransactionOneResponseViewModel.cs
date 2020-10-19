// Copyright (c) 2020 Bitcoin Association

using System.Linq;
using System.Text.Json.Serialization;
using MerchantAPI.APIGateway.Domain.Models;

namespace MerchantAPI.APIGateway.Rest.ViewModels
{
  public class SubmitTransactionOneResponseViewModel
  {
    public SubmitTransactionOneResponseViewModel()
    {
    }

    public SubmitTransactionOneResponseViewModel(SubmitTransactionOneResponse domain)
    {
      Txid = domain.Txid;
      ReturnResult = domain.ReturnResult ?? "";
      ResultDescription = domain.ResultDescription ?? "";
      ConflictedWith = domain.ConflictedWith?.Select(t => new SubmitTransactionConflictedTxResponseViewModel(t)).ToArray();
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
