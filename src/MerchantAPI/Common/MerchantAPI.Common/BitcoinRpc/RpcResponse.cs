// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.Common.BitcoinRpc.Responses;
using System.Text.Json.Serialization;

namespace MerchantAPI.Common.BitcoinRpc
{
  public class RpcResponse<T>
  {
    [JsonPropertyName("result")]
    public T Result { get; set; }

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("error")]
    public RpcError Error { get; set; }      
  }
}
