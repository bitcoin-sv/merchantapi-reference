// Copyright (c) 2020 Bitcoin Association

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace MerchantAPI.APIGateway.Test.Functional.CallBackWebServer
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
    public async Task ProcessPost()
    {
      var ms = new MemoryStream();
      await HttpContext.Request.Body.CopyToAsync(ms);
      callBackReceived.CallbackReceived(Request.Path, ms.ToArray());
      Response.StatusCode = 200;
    }


    /// <summary>
    /// Provide GET endpoint for troubleshooting purposes
    /// </summary>
    /// <returns></returns>
    [HttpGet]
    public string ProcessGet()
    {
      return "This is mAPI test callback controller";
    }
  }
}
