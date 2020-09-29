// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace MerchantAPI.APIGateway.Test.Stress.CallBackWebServer
{

  /// <summary>
  /// Controller that redirects incoming POST requests to ICallBackReceived
  /// </summary>
  [Route("/CallBack")]
  [ApiController]
  public class CallBackController : ControllerBase
  {
    readonly ICallBackReceived callBackReceived;
    public CallBackController(ICallBackReceived callBackReceived)
    {
      this.callBackReceived = callBackReceived ?? throw new ArgumentException(nameof(callBackReceived));
    }

    [HttpPost]
    public void ProcessPost()
    {
      callBackReceived.CallbackReceived(HttpContext);
    }


    /// <summary>
    /// Provide GET endpoint for troubleshooting purposes
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public string ProcessGet()
    {
      return "This is mAPI stress test callback controller";
    }
  }
}
