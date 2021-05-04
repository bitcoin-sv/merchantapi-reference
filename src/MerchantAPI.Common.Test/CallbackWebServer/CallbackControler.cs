// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MerchantAPI.Common.Test.CallbackWebServer
{

  /// <summary>
  /// Controller that redirects incoming POST requests to ICallbackReceived
  /// </summary>
  [Route("/Callback")]
  [ApiController]
  [ApiExplorerSettings(IgnoreApi = true)]
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
