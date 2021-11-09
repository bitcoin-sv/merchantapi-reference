// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using System.Net;

namespace MerchantAPI.APIGateway.Rest
{
  public class HttpsRequiredAttribute : ActionFilterAttribute
  {
    public override void OnActionExecuting(ActionExecutingContext context)
    {
      IOptions<AppSettings> appSettings = (IOptions<AppSettings>) context.HttpContext.RequestServices.GetService(typeof(IOptions<AppSettings>));

      if (Startup.HostEnvironment.EnvironmentName != "Testing" && !context.HttpContext.Request.IsHttps && !appSettings.Value.EnableHTTP.Value)
      {
        context.Result = new StatusCodeResult((int)HttpStatusCode.BadRequest);
        return;
      }

      base.OnActionExecuting(context);
    }
  }
}
