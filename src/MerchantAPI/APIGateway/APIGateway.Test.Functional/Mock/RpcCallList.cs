// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MerchantAPI.APIGateway.Test.Functional.Mock
{
  public class RpcCallList
  {
    readonly object objLock = new object();
    readonly List<Call> Calls = new List<Call>(); 

    public void AddCall(string methodName, string nodeId, string txIds = null)
    {
      lock (objLock)
      {
        Calls.Add(new Call(methodName, nodeId, txIds));
      }
    }

    public void ClearCalls()
    {
      lock (objLock)
      {
        Calls.Clear();
      }
    }

    public void AssertEqualTo(params string[] expected)
    {
      Assert.AreEqual(string.Join(Environment.NewLine, expected), ToString());
    }

    public void AssertContains(string prefix, params string[] expected)
    {
      Assert.AreEqual(string.Join(Environment.NewLine, expected), ToString(prefix));
    }

    public IEnumerable<Call> FilterCalls(string prefix)
    {
      Call[] callsArray;
      lock (objLock)
      {
        callsArray = Calls.ToArray();
      }

      foreach (var call in callsArray)
      {
        var s = call.ToString();
        if (prefix == null || s.StartsWith(prefix))
        {
          yield return call;
        }
      }
    }

    public string ToString(string prefix)
    {
      lock (objLock)
      {
        var sb = new StringBuilder();
        foreach (var call in FilterCalls(prefix))
        {
          sb.AppendLine(call.ToString());
        }

        return sb.ToString().Trim(); // trim ending newline
      }
    }

    public override string ToString()
    {
      return ToString(null);
    }

    public class Call
    {
      public Call(string methodName, string nodeId, string txIds = null)
      {
        MethodName = methodName;
        NodeId = nodeId;
        TxIds = txIds;
      }

      public readonly string TxIds;
      public readonly string NodeId;
      public readonly string MethodName;

      public override string ToString()
      {
        var result = NodeId + ":" + MethodName;
        if (TxIds != null)
        {
          result += "/" + TxIds;
        }
        return result;
      }
    }
  }
}
