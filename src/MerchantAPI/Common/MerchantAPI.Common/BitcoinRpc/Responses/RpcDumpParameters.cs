// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace MerchantAPI.Common.BitcoinRpc.Responses
{
  public class RpcDumpParameters
  {
    // defaults must be in sync with bitcoind's default settings
    private const int DEFAULT_HTTP_SERVER_TIMEOUT = 30; // seconds
    private const uint DEFAULT_MEMPOOL_EXPIRY = 336; // hours

    [JsonPropertyName("rpcservertimeout")]
    public int RpcServerTimeout { get; private set; }

    [JsonPropertyName("mempoolexpiry")]
    public uint? MempoolExpiry { get; private set; }

    // there are additional fields that are not mapped
    public RpcDumpParameters() : this(Array.Empty<string>())
    {
    }

    public RpcDumpParameters(string[] parameters)
    {
      var paramsDict = parameters.Where(x => x.Contains("=")).Select(item => item.Split('='))
                                 .ToLookup(item => item[0].ToLower(), item => (object)item[1]).ToDictionary(s => s.Key, s => s.First());
      RpcServerTimeout = GetParamValue(paramsDict, nameof(RpcServerTimeout).ToLower(), DEFAULT_HTTP_SERVER_TIMEOUT); 
      MempoolExpiry = GetParamValue(paramsDict, nameof(MempoolExpiry).ToLower(), DEFAULT_MEMPOOL_EXPIRY);
    }

    private static T GetParamValue<T>(Dictionary<string, object> paramsDict, string paramName, T defaultValue)
    {
      if (paramsDict?.ContainsKey(paramName) == true)
      {
        return (T)Convert.ChangeType(paramsDict[paramName], typeof(T));
      }
      return defaultValue;
    }
  }
}
