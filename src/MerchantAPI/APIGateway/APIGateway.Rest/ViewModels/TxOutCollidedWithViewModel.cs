// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain.Models;
using System.Text.Json.Serialization;

namespace MerchantAPI.APIGateway.Rest.ViewModels
{
  public class TxOutCollidedWithViewModel
  {
    public TxOutCollidedWithViewModel()
    {
    }

    public TxOutCollidedWithViewModel(TxOutCollidedWith domain)
    {
      TxId = domain.TxId;
      Size = domain.Size;
      Hex = domain.Hex;
    }

    [JsonPropertyName("txid")]
    public string TxId { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("hex")]
    public string Hex { get; set; }
  }
}
