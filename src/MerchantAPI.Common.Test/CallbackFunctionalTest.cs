// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MerchantAPI.Common.Test.CallbackWebServer;
using Microsoft.AspNetCore.Http;

namespace MerchantAPI.Common.Test
{

  /// <summary>
  /// Traces all callbacks received through mocked call back server
  /// </summary>
  public class CallbackFunctionalTests : ICallbackReceived 
  {
    readonly object lockObj = new();

    List<(string path, string request)> calls = new();
    public string Url => "http://mockCallback:8321";


    public (string path, string request)[] Calls
    {
      get
      {
        lock (lockObj)
        {
          return calls.ToArray();
        }
      }
    }

    public Task CallbackReceivedAsync(string path, IHeaderDictionary headers, byte[] data)
    {

      lock (lockObj)
      {
        var str = new StreamReader(new MemoryStream(data));
        calls.Add((path, str.ReadToEnd()));
      }

      return Task.CompletedTask;
    }
  }
}
