// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain.Models;
using System.Text.Json.Serialization;

namespace MerchantAPI.APIGateway.Rest.ViewModels
{
  public class TxOutViewModel
  {
    public TxOutViewModel()
    {
    }

    public TxOutViewModel(TxOutResponse domain)
    {
      Error = domain.Error;
      if (domain.CollidedWith != null)
      {
        CollidedWith = new TxOutCollidedWithViewModel(domain.CollidedWith);
      }
      if (string.IsNullOrEmpty(domain.Error))
      {
        ScriptPubKey = domain.ScriptPubKey;
        ScriptPubKeyLen = domain.ScriptPubKeyLen;
        Value = domain.Value;
        IsStandard = domain.IsStandard;
        Confirmations = domain.Confirmations;
      }
    }

    [JsonPropertyName("error")]
    public string Error { get; set; }

    [JsonPropertyName("collidedWith")]
    public TxOutCollidedWithViewModel CollidedWith { get; set; }

    [JsonPropertyName("scriptPubKey")]
    public string ScriptPubKey { get; set; }

    [JsonPropertyName("scriptPubKeyLen")]
    public long? ScriptPubKeyLen { get; set; }

    [JsonPropertyName("value")]
    public decimal? Value { get; set; }

    [JsonPropertyName("isStandard")]
    public bool? IsStandard { get; set; }

    [JsonPropertyName("confirmations")]
    public long? Confirmations { get; set; }
  }
}
