// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using System.Linq;
using System.Text.Json.Serialization;
using MerchantAPI.APIGateway.Domain;
using MerchantAPI.APIGateway.Domain.Models;

namespace MerchantAPI.APIGateway.Rest.ViewModels
{
  public class SubmitTransactionsResponseViewModel
  {

    [JsonPropertyName("apiVersion")]
    public string ApiVersion { get; set; }


    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("minerId")]
    public string MinerId { get; set; }

    [JsonPropertyName("currentHighestBlockHash")]
    public string CurrentHighestBlockHash { get; set; }

    [JsonPropertyName("currentHighestBlockHeight")]
    public long CurrentHighestBlockHeight { get; set; }
    
    [JsonPropertyName("txSecondMempoolExpiry")]
    public long TxSecondMempoolExpiry { get; set; } 

    [JsonPropertyName("txs")]
    public SubmitTransactionOneResponseViewModel[] Txs { get; set; }

    [JsonPropertyName("failureCount")]
    public long FailureCount { get; set; }

    public SubmitTransactionsResponseViewModel()
    {
    }
    public SubmitTransactionsResponseViewModel(SubmitTransactionsResponse domain)
    {
      ApiVersion = Const.MERCHANT_API_VERSION;
      Timestamp = domain.Timestamp;
      MinerId = domain.MinerId;
      CurrentHighestBlockHash = domain.CurrentHighestBlockHash;
      CurrentHighestBlockHeight = domain.CurrentHighestBlockHeight;
      TxSecondMempoolExpiry = domain.TxSecondMempoolExpiry;
      Txs = domain.Txs.Select(x => new SubmitTransactionOneResponseViewModel(x)).ToArray();
      FailureCount = domain.FailureCount;
    }

  }
}
