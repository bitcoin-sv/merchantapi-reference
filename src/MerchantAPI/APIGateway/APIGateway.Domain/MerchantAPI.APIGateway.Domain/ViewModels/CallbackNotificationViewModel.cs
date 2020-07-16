// Copyright (c) 2020 Bitcoin Association

using System;
using System.Text.Json.Serialization;

namespace MerchantAPI.APIGateway.Domain.ViewModels
{
  public class CallbackNotificationViewModel
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
    
    [JsonPropertyName("callbackPayload")]
    public object CallbackPayload { get; set; }
  }
}
