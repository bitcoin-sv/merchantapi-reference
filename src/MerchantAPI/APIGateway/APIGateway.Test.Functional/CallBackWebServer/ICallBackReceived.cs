// Copyright (c) 2020 Bitcoin Association

using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace MerchantAPI.APIGateway.Test.Functional.CallbackWebServer
{
  public interface ICallbackReceived
  {
    public Task CallbackReceivedAsync(string url, IHeaderDictionary headers, byte[] data);
  }

}
