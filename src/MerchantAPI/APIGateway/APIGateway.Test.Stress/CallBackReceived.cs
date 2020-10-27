// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MerchantAPI.APIGateway.Domain.ViewModels;
using MerchantAPI.APIGateway.Test.Functional.CallBackWebServer;
using MerchantAPI.Common.Json;
using Microsoft.AspNetCore.Http;
using NBitcoin;

namespace MerchantAPI.APIGateway.Test.Stress
{

  /// <summary>
  /// Implementation if ICallBackReceived that just counts call backs
  /// </summary>
  public class CallBackReceived : ICallBackReceived
  {
    readonly Stats stats;
    readonly Dictionary<string, CallbackHostConfig> callbackHostConfig;
    Random rnd = new Random();

    public CallBackReceived(Stats stats, CallbackHostConfig[] callbackHostConfig)
    {
      this.stats = stats;

      if (callbackHostConfig != null)
      {
        this.callbackHostConfig =
          callbackHostConfig.ToDictionary(x => x.HostName, x => x, StringComparer.InvariantCultureIgnoreCase);
      }
    }

    public async Task CallbackReceivedAsync(string path, IHeaderDictionary headers, byte[] data)
    {
      string host = "";
      if (headers.TryGetValue("Host", out var hostValues))
      {
        host = hostValues[0].Split(":")[0]; // chop off port
      }

      if (callbackHostConfig != null)
      {
        if (!callbackHostConfig.TryGetValue(host, out var hostConfig))
        {
          // Retry with empty string that represents the default host
          callbackHostConfig.TryGetValue("", out hostConfig);
        }

        if (hostConfig != null)
        {
          if (hostConfig.CallBackFailurePercent > 0)
          {
            if (rnd.Next(0, 100) < hostConfig.CallBackFailurePercent)
            {
              stats.IncrementSimulatedCallbackErrors();
              throw new Exception("Stress test tool intentionally failing callback");
            }
          }

          if (hostConfig.MinCallbackDelayMs != null || hostConfig.MaxCallbackDelayMs != null)
          {
            // If only one value is present then copy the value from the other one
            int min = (int) (hostConfig.MinCallbackDelayMs ?? hostConfig.MaxCallbackDelayMs);
            int max = (int) (hostConfig.MaxCallbackDelayMs ?? hostConfig.MinCallbackDelayMs);
            int delay = rnd.Next(Math.Min(min, max), Math.Max(min, max));
            await Task.Delay(delay);
          }
        }
      }

      // assume that responses are signed
      // TODO: decrypting is not currently supported
      var payload = HelperTools.JSONDeserialize<SignedPayloadViewModel>(Encoding.UTF8.GetString(data))
        .Payload;

      var notification = HelperTools.JSONDeserialize<CallbackNotificationViewModelBase>(payload);

      stats.IncrementCallbackReceived(host, new uint256(notification.CallbackTxId));
    }
  }
}
