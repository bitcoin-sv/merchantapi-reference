// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System.Text.Json.Serialization;

namespace MerchantAPI.APIGateway.Rest.ViewModels
{
  public class TxOutsRequestViewModel
  {
    [JsonPropertyName("txid")]
    public string TxId { get; set; }

    [JsonPropertyName("n")]
    public long N { get; set; }
  }
}
