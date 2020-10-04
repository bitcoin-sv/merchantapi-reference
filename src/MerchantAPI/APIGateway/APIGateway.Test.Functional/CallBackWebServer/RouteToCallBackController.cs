// Copyright (c) 2020 Bitcoin Association

using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;

namespace MerchantAPI.APIGateway.Test.Functional.CallBackWebServer
{
  
  /// <summary>
  /// Route all calls (regardless of path) to CallBackController
  /// </summary>
  public class RouteToCallBackController : DynamicRouteValueTransformer
  {
    public override ValueTask<RouteValueDictionary> TransformAsync(HttpContext httpContext, RouteValueDictionary values)
    {
      var result = new RouteValueDictionary()
      {
        {"controller", "CallBack"},
        {"action", "ProcessPost"}
      };

      return new ValueTask<RouteValueDictionary>(result);
    }
  }
}
