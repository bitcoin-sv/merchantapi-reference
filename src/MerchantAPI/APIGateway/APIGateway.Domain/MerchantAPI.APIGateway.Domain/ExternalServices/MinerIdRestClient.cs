// Copyright (c) 2020 Bitcoin Association

using System;
using System.Net.Http;
using System.Threading.Tasks;
using MerchantAPI.APIGateway.Domain.Actions;
using MerchantAPI.Common;
using MerchantAPI.Common.Json;

namespace MerchantAPI.APIGateway.Domain.ExternalServices
{

  public class MinerIdRestClient : IMinerId
  {
    bool? supportPassingInSigningPublicKey; 
    object lockObj = new object();
    readonly RestClient restClient;
    public MinerIdRestClient(string minerIdUrl, string minerIdAlias, string authorization, HttpClient httpClient)
    {
      if (minerIdUrl == null)
      {
        throw new ArgumentNullException(nameof(minerIdUrl));
      }

      if (minerIdAlias == null)
      {
        throw new ArgumentNullException(nameof(minerIdAlias));
      }

      if (httpClient == null)
      {
        throw new ArgumentNullException(nameof(httpClient));
      }
      var url = minerIdUrl.TrimEnd('/') +"/"+ minerIdAlias;
      restClient = new RestClient(url, authorization, httpClient);
    }

    public async Task<string> GetCurrentMinerIdAsync()
    {
      return await restClient.GetStringAsync("");
    }

    string RefreseHash(string hash)
    {
      var array = HelperTools.HexStringToByteArray(hash);
      Array.Reverse(array);
      return HelperTools.ByteToHexString(array);
    }
    public async Task<string> SignWithMinerIdAsync(string currentMinerId, string hash)
    {
      
      hash = RefreseHash(hash); // MinerId endpoint expect hash in reversed order
      bool useMinerIdInUrl = false;
      bool tryWithMinerIdInUrl = false;

      string urlWithMinerId = $"/sign/{hash}/{currentMinerId}";
      string urlWithoutMinerId = $"/sign/{hash}";

      // try to determine if endpoint support passing in public key in addition to hash
      lock (lockObj)
      {
        if (!supportPassingInSigningPublicKey.HasValue)
        {
          // we can not do async call while holding a lock. Take note that we need to call it later.
          tryWithMinerIdInUrl = true;
        }
        else
        {
          useMinerIdInUrl = supportPassingInSigningPublicKey.Value;
        }
      }

      if (tryWithMinerIdInUrl)
      {
        try
        {
          var result = await restClient.GetStringAsync(urlWithMinerId);
          lock (lockObj)
          {
            supportPassingInSigningPublicKey = true;
            return result;
          }
        }
        catch (NotFoundException) // 404
        {
          lock (lockObj)
          {
            supportPassingInSigningPublicKey = false;
          }
        }
      }

      return await restClient.GetStringAsync(useMinerIdInUrl ? urlWithMinerId : urlWithoutMinerId);
    }
  }
}
