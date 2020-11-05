// Copyright (c) 2020 Bitcoin Association

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MerchantAPI.APIGateway.Test.Functional.CallbackWebServer
{

  /// <summary>
  /// Controller that redirects incoming POST requests to ICallbackReceived
  /// </summary>
  [Route("/Callback")]
  [ApiController]
  public class CallbackController : ControllerBase
  {
    readonly ICallbackReceived callbackReceived;
    public CallbackController(ICallbackReceived callbackReceived)
    {
      this.callbackReceived = callbackReceived ?? throw new ArgumentException(nameof(callbackReceived));
    }

    [HttpPost]
    public async Task ProcessPost()
    {
      var ms = new MemoryStream();
      await HttpContext.Request.Body.CopyToAsync(ms);
      await callbackReceived.CallbackReceivedAsync(Request.Path, Request.Headers, ms.ToArray());
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
