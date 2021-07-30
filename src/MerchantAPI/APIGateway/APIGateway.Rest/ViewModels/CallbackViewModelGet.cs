// Copyright(c) 2021 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System.Text.Json.Serialization;

namespace MerchantAPI.APIGateway.Rest.ViewModels
{
  public class CallbackViewModelGet
  {
    [JsonPropertyName("IPaddress")]
    public string IPaddress { get; set; } 

    public CallbackViewModelGet() { }

    public CallbackViewModelGet(string url)
    {
      IPaddress = url;
    }
  }
}
