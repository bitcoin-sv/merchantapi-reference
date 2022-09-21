// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.Common.Json;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;

namespace MerchantAPI.Common.BitcoinRpc
{
  public class RpcRequest
  {
    [JsonPropertyName("method")]
    public string Method { get; set; }

    [JsonPropertyName("params")]
    public IList<object> Parameters { get; set; }

    [JsonPropertyName("id")]
    public int Id { get; set; }

    public RpcRequest(int id, string method, params object[] parameters)
    {
      Id = id;
      Method = method;

      if (parameters != null)
      {
        Parameters = parameters.ToList();
      }
      else
      {
        Parameters = new List<object>();
      }
    }

    public string GetJSON()
    {
      return HelperTools.JSONSerialize(this, false);
    }

    public byte[] GetBytes()
    {      
      return Encoding.UTF8.GetBytes(GetJSON());
    }
  }
}
