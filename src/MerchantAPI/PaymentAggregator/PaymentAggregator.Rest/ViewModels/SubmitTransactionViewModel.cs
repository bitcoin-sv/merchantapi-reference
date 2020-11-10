// Copyright (c) 2020 Bitcoin Association

using System.Text.Json.Serialization;

namespace MerchantAPI.PaymentAggregator.Rest.ViewModels
{
  public class SubmitTransactionViewModel
  {
    [JsonPropertyName("rawTx")]
    public string RawTx { get; set; }

    [JsonPropertyName("callbackUrl")]
    public string CallbackUrl { get; set; }

    [JsonPropertyName("callbackToken")]
    public string CallbackToken { get; set; }

    [JsonPropertyName("callbackEncryption")]
    public string CallbackEncryption { get; set; }

    [JsonPropertyName("merkleProof")]
    public bool? MerkleProof { get; set; }

    [JsonPropertyName("dsCheck")]
    public bool? DsCheck { get; set; }   
  }
}
