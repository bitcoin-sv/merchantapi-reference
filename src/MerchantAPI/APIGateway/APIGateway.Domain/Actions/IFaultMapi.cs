// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.Common.BitcoinRpc.Responses;
using MerchantAPI.Common.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static MerchantAPI.Common.BitcoinRpc.Responses.RpcSendTransactions;

namespace MerchantAPI.APIGateway.Domain.Actions
{
  public interface IFaultMapi
  {
    Task<(RpcSendTransactions, Exception)> SendRawTransactions(
      (byte[] transaction, bool allowhighfees, bool dontCheckFees, bool listUnconfirmedAncestors, Dictionary<string, object> config)[] transactions,
      Faults.FaultType faultType);

    protected static RpcSendTransactions CreateRpcInvalidResponse(byte[][] transactions, string rejectCodeAndReason)
    {
      var rpcResult = new RpcSendTransactions();
      List<RpcInvalidTx> txsInvalid = new();
      foreach (var transaction in transactions)
      {
        var tx = HelperTools.ParseBytesToTransaction(transaction);
        RpcInvalidTx txInvalid = new()
        {
          RejectCode = int.Parse(rejectCodeAndReason.Split(" ")[0]),
          RejectReason = rejectCodeAndReason.Split(" ", 2)[1],
          Txid = tx.GetHash().ToString()
        };
        txsInvalid.Add(txInvalid);
      }
      rpcResult.Invalid = txsInvalid.ToArray();
      return rpcResult;
    }

    protected static RpcSendTransactions CreateRpcEvictedResponse(byte[][] transactions)
    {
      var rpcResult = new RpcSendTransactions();
      List<string> txsEvicted = new();
      foreach (var transaction in transactions)
      {
        var tx = HelperTools.ParseBytesToTransaction(transaction);
        txsEvicted.Add(tx.GetHash().ToString());
      }
      rpcResult.Evicted = txsEvicted.ToArray();
      return rpcResult;
    }
  }
}
