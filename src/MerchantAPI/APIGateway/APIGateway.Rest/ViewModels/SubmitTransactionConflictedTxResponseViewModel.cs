// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System.Text.Json.Serialization;
using MerchantAPI.APIGateway.Domain.Models;

namespace MerchantAPI.APIGateway.Rest.ViewModels
{
  public class SubmitTransactionConflictedTxResponseViewModel
  {
    public SubmitTransactionConflictedTxResponseViewModel()
    {
    }

    public SubmitTransactionConflictedTxResponseViewModel(SubmitTransactionConflictedTxResponse domain)
    {
      Txid = domain.Txid;
      Size = domain.Size;
      Hex = domain.Hex;
    }

    [JsonPropertyName("txid")]
    public string Txid { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("hex")]
    public string Hex { get; set; }
  }
}
