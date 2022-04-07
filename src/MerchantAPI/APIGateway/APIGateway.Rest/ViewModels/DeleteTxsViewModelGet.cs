// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain.Models;
using System.Linq;
using System.Text.Json.Serialization;

namespace MerchantAPI.APIGateway.Rest.ViewModels
{
  public class DeleteTxsViewModelGet
  {
    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("txIds")]
    public string[] TxIds { get; set; }

    public DeleteTxsViewModelGet() { }

    public DeleteTxsViewModelGet(Tx[] txs)
    {
      TxIds = txs.Select(x => x.TxExternalId.ToString()).ToArray();
      Count = txs.Length;
    }
  }
}
