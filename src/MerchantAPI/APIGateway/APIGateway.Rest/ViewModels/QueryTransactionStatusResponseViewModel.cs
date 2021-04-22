// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using System.Text.Json.Serialization;
using MerchantAPI.APIGateway.Domain;
using MerchantAPI.APIGateway.Domain.Models;

namespace MerchantAPI.APIGateway.Rest.ViewModels
{
  public class QueryTransactionStatusResponseViewModel
  {

    public QueryTransactionStatusResponseViewModel()
    {
    }

    public QueryTransactionStatusResponseViewModel(QueryTransactionStatusResponse domain)
    {
      ApiVersion = Const.MERCHANT_API_VERSION;
      Timestamp = domain.Timestamp;
      Txid = domain.Txid;
      ReturnResult = domain.ReturnResult;
      ResultDescription = domain.ResultDescription;
      BlockHash = domain.BlockHash;
      BlockHeight = domain.BlockHeight;
      Confirmations = domain.Confirmations;
      MinerId = domain.MinerID;
      TxSecondMempoolExpiry = domain.TxSecondMempoolExpiry;
    }


    [JsonPropertyName("apiVersion")]
    public string ApiVersion { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("txid")]
    public string Txid { get; set; }

    [JsonPropertyName("returnResult")]
    public string ReturnResult { get; set; }

    [JsonPropertyName("resultDescription")]
    public string ResultDescription { get; set; }

    [JsonPropertyName("blockHash")]
    public string BlockHash { get; set; }

    [JsonPropertyName("blockHeight")]
    public long? BlockHeight { get; set; }

    [JsonPropertyName("confirmations")]
    public long? Confirmations { get; set; }

    [JsonPropertyName("minerId")]
    public string MinerId { get; set; }

    [JsonPropertyName("txSecondMempoolExpiry")]
    public int TxSecondMempoolExpiry { get; set; }
  }
}
