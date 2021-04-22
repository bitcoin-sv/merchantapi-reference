// Copyright (c) 2020 Bitcoin Association

using System.Text.Json.Serialization;
using MerchantAPI.APIGateway.Domain.Models;
using MerchantAPI.Common.Json;

namespace MerchantAPI.APIGateway.Rest.ViewModels
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

    [JsonPropertyName("merkleFormat")]
    public string MerkleFormat { get; set; }

    [JsonPropertyName("dsCheck")]
    public bool? DsCheck { get; set; }

    public SubmitTransaction ToDomainModel(string defaultCallbackUrl, string defaultCallbackToken, string defaultCallbackEncryption, bool defaultMerkleProof, string defaultMerkleFormat,  bool defaultDsCheck)
    {
      return new SubmitTransaction
      {
        RawTxString = RawTx,
        CallbackUrl = CallbackUrl ?? defaultCallbackUrl,
        CallbackToken = CallbackToken ?? defaultCallbackToken,
        CallbackEncryption =  CallbackEncryption ?? defaultCallbackEncryption,
        MerkleProof = MerkleProof ?? defaultMerkleProof,
        MerkleFormat = MerkleFormat ?? defaultMerkleFormat,
        DsCheck = DsCheck ?? defaultDsCheck
      };

    }
  }
}
