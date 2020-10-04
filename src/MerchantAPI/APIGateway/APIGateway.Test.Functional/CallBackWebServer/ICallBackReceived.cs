// Copyright (c) 2020 Bitcoin Association

using Microsoft.AspNetCore.Http;

namespace MerchantAPI.APIGateway.Test.Functional.CallBackWebServer
{
  public interface ICallBackReceived
  {
    public void CallbackReceived(HttpContext ctx);
  }

}
