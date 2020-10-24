// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;
using System.Text;
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
    public CallBackReceived(Stats stats)
    {
      this.stats = stats;
    }
    public void CallbackReceived(string path, IHeaderDictionary headers, byte[] data)
    {
      string host = "";
      if (headers.TryGetValue("Host", out var hostValues))
      {
        host = hostValues[0].Split(":")[0]; // chop off port
      }

      // assume that responses are signed
      // TODO: decrypting is not currently supported
      var payload = HelperTools.JSONDeserializeNewtonsoft<SignedPayloadViewModel>(Encoding.UTF8.GetString(data))
        .Payload;

      var notification = HelperTools.JSONDeserializeNewtonsoft<CallbackNotificationViewModelBase>(payload);

      stats.IncrementCallbackReceived(host, new uint256(notification.CallbackTxId));
    }
  }
}
