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

    [JsonPropertyName("callBackUrl")]
    public string CallBackUrl { get; set; }

    [JsonPropertyName("callBackToken")]
    public string CallBackToken { get; set; }

    [JsonPropertyName("callBackEncryption")]
    public string CallBackEncryption { get; set; }

    [JsonPropertyName("merkleProof")]
    public bool? MerkleProof { get; set; }
    
    [JsonPropertyName("dsCheck")]
    public bool? DsCheck { get; set; }

    public SubmitTransaction ToDomainModel(string defaultCallBackUrl, string defaultCallBackToken, string defaultCallBackEncryption, bool defaultMerkleProof, bool defaultDsCheck)
    {
      return new SubmitTransaction
      {
        RawTx = HelperTools.HexStringToByteArray(RawTx),
        CallBackUrl = CallBackUrl ?? defaultCallBackUrl,
        CallBackToken = CallBackToken ?? defaultCallBackToken,
        CallBackEncryption =  CallBackEncryption ?? defaultCallBackEncryption,
        MerkleProof = MerkleProof ?? defaultMerkleProof,
        DsCheck = DsCheck ?? defaultDsCheck
      };

    }
  }
}
