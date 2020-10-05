// Copyright (c) 2020 Bitcoin Association

using System;
using System.Text.Json.Serialization;
using MerchantAPI.Common.BitcoinRpc.Responses;

namespace MerchantAPI.APIGateway.Domain.ViewModels
{
  /// <summary>
  /// Base class containing fields common to all callbacks.
  /// Derived classes contains actual payload.
  /// </summary>
  public class CallbackNotificationViewModelBase
  {
    [JsonPropertyName("apiVersion")]
    public string APIVersion { get; set; }
    
    [JsonPropertyName("timeStamp")]
    public DateTime TimeStamp { get; set; }
    
    [JsonPropertyName("minerId")]
    public string MinerId { get; set; }
    
    [JsonPropertyName("BlockHash")]
    public string BlockHash { get; set; }
    
    [JsonPropertyName("BlockHeight")]
    public long BlockHeight { get; set; }
    
    [JsonPropertyName("callbackTxId")]
    public string CallbackTxId { get; set; }

    [JsonPropertyName("callbackReason")]
    public string CallbackReason { get; set; }

  }

  public class CallbackNotificationMerkeProofViewModel : CallbackNotificationViewModelBase
  {
    [JsonPropertyName("callbackPayload")]
    public RpcGetMerkleProof CallbackPayload { get; set; }
  }

  public class CallbackNotificationDoubleSpendViewModel : CallbackNotificationViewModelBase
  {
    [JsonPropertyName("callbackPayload")]
    public DsNotificationPayloadCallBackViewModel CallbackPayload { get; set; }
  }

  public class DsNotificationPayloadCallBackViewModel
  {
    [JsonPropertyName("doubleSpendTxId")]
    public string DoubleSpendTxId { get; set; }

    [JsonPropertyName("payload")]
    public string Payload { get; set; }
  }

}
