// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace MerchantAPI.Common.Test.CallbackWebServer
{
  public interface ICallbackReceived
  {
    public Task CallbackReceivedAsync(string url, IHeaderDictionary headers, byte[] data);
  }

}
