// Copyright(c) 2021 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain.Models.APIStatus;
using System.Text.Json.Serialization;

namespace MerchantAPI.APIGateway.Rest.ViewModels.APIStatus
{
  public class SubmitTxStatusViewModel
  {
    [JsonPropertyName("request")]
    public double Request { get; set; }
    [JsonPropertyName("txAuthenticatedUser")]
    public double TxAuthenticatedUser { get; set; }
    [JsonPropertyName("txAnonymousUser")]
    public double TxAnonymousUser { get; set; }
    [JsonPropertyName("tx")]
    public double Tx { get; set; }
    [JsonPropertyName("avgBatch")]
    public double AvgBatch { get; set; }
    [JsonPropertyName("txSentToNode")]
    public double TxSentToNode { get; set; }
    [JsonPropertyName("txAcceptedByNode")]
    public double TxAcceptedByNode { get; set; }
    [JsonPropertyName("txRejectedByNode")]
    public double TxRejectedByNode { get; set; }
    [JsonPropertyName("txSubmitException")]
    public double TxSubmitException { get; set; }
    [JsonPropertyName("txResponseSuccess")]
    public double TxResponseSuccess { get; set; }
    [JsonPropertyName("txResponseFailure")]
    public double TxResponseFailure { get; set; }
    [JsonPropertyName("txResponseException")]
    public double TxResponseException { get; set; }
    [JsonPropertyName("submitTxDescription")]
    public string SubmitTxDescription { get; set; }

    public SubmitTxStatusViewModel(SubmitTxStatus submitTxStatus)
    {
      Request = submitTxStatus.Request;
      TxAuthenticatedUser = submitTxStatus.TxAuthenticatedUser;
      TxAnonymousUser = submitTxStatus.TxAnonymousUser;
      Tx = submitTxStatus.Tx;
      AvgBatch = submitTxStatus.AvgBatch;
      TxSentToNode = submitTxStatus.TxSentToNode;
      TxAcceptedByNode = submitTxStatus.TxAcceptedByNode;
      TxRejectedByNode = submitTxStatus.TxRejectedByNode;
      TxSubmitException = submitTxStatus.TxSubmitException;
      TxResponseSuccess = submitTxStatus.TxResponseSuccess;
      TxResponseFailure = submitTxStatus.TxResponseFailure;
      TxResponseException = submitTxStatus.TxResponseException;
      SubmitTxDescription = submitTxStatus.SubmitTxDescription;
    }
  }
}
