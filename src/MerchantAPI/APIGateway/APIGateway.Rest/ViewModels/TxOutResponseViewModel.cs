// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain.Models;
using MerchantAPI.APIGateway.Domain;
using System;
using System.Text.Json.Serialization;
using System.Linq;

namespace MerchantAPI.APIGateway.Rest.ViewModels
{
  public class TxOutsResponseViewModel
  {
    public TxOutsResponseViewModel()
    {
    }
    
    public TxOutsResponseViewModel(TxOutsResponse domain)
    {
      ApiVersion = Const.MERCHANT_API_VERSION;
      Timestamp = domain.Timestamp;
      MinerId = domain.MinerID;
      ReturnResult = domain.ReturnResult;
      ResultDescription = domain.ResultDescription;
      TxOuts = domain.TxOuts?.Select(x => new TxOutViewModel(x)).ToArray();
    }    

    [JsonPropertyName("apiVersion")]
    public string ApiVersion { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("minerId")]
    public string MinerId { get; set; }

    [JsonPropertyName("returnResult")]
    public string ReturnResult { get; set; }

    [JsonPropertyName("resultDescription")]
    public string ResultDescription { get; set; }

    [JsonPropertyName("txouts")]
    public TxOutViewModel[] TxOuts { get; set; }


  }
}
