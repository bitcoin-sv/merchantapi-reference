// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace MerchantAPI.APIGateway.Test.Stress.CallBackWebServer
{
  public interface ICallBackReceived
  {
    public void CallbackReceived(HttpContext ctx);
  }

}
