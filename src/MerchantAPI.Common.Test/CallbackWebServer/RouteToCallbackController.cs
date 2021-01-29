// Copyright (c) 2020 Bitcoin Association

using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;

namespace MerchantAPI.Common.Test.CallbackWebServer
{
  
  /// <summary>
  /// Route all calls (regardless of path) to CallbackController
  /// </summary>
  public class RouteToCallbackController : DynamicRouteValueTransformer
  {
    public override ValueTask<RouteValueDictionary> TransformAsync(HttpContext httpContext, RouteValueDictionary values)
    {
      var result = new RouteValueDictionary()
      {
        {"controller", "Callback"},
        {"action", "ProcessPost"}
      };

      return new ValueTask<RouteValueDictionary>(result);
    }
  }
}
