using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Net;

namespace MerchantAPI.APIGateway.Rest
{
  public class HttpsRequiredAttribute : ActionFilterAttribute
  {
    public override void OnActionExecuting(ActionExecutingContext context)
    {
      if (Startup.HostEnvironment.EnvironmentName != "Testing" && !context.HttpContext.Request.IsHttps)
      {
        context.Result = new StatusCodeResult((int)HttpStatusCode.BadRequest);
        return;
      }

      base.OnActionExecuting(context);
    }
  }
}
