// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;

namespace MerchantAPI.APIGateway.Domain.DSAccessChecks
{
  public class CheckHostActionFilter : ActionFilterAttribute
  {
    readonly IHostBanList banList;

    public CheckHostActionFilter(IHostBanList banList)
    {
      this.banList = banList ?? throw new ArgumentNullException(nameof(banList));
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
      var requestHost = context.HttpContext.Request.Host.Host;

      if (banList.IsHostBanned(requestHost))
      {
        context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
        return;
      }
      base.OnActionExecuting(context);
    }
  }
}
