// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MerchantAPI.Common.BitcoinRpc;
using MerchantAPI.Common.BitcoinRpc.Responses;

namespace MerchantAPI.APIGateway.Domain.Models
{
  /// <summary>
  /// Handles RPC calls to multiple nodes.
  /// </summary>
  public interface IRpcMultiClient
  {

    Task<RpcSendTransactions> SendRawTransactionsAsync(
      (byte[] transaction, bool allowhighfees, bool dontCheckFees, bool listUnconfirmedAncestors, Dictionary<string, object> config)[] transactions);

    Task<byte[]> GetRawTransactionAsBytesAsync(string txId);
    Task<RpcGetBlockchainInfo> GetWorstBlockchainInfoAsync();
    Task<RpcGetBlockchainInfo> GetBestBlockchainInfoAsync();
    Task<(RpcGetRawTransaction firstOkResult, bool allOkTheSame, Exception firstError)> GetRawTransactionAsync(string id);
    Task<RpcGetMerkleProof> GetMerkleProofAsync(string txId, string blockHash);
    Task<RpcGetMerkleProof2> GetMerkleProof2Async(string blockHash, string txId);
    Task<RpcBitcoinStreamReader> GetBlockAsStreamAsync(string blockHash, CancellationToken? token = null);
    Task<RpcGetBlockHeader> GetBlockHeaderAsync(string blockHash);
    Task<RpcGetNetworkInfo> GetAnyNetworkInfoAsync();
    Task<RpcGetTxOuts> GetTxOutsAsync(IEnumerable<(string txId, long N)> outpoints, string[] fieldList);
    Task<RpcVerifyScriptResponse[]> VerifyScriptAsync(bool stopOnFirstInvalid,
                                                      int totalTimeoutSec,
                                                      IEnumerable<(string Tx, int N)> dsTx);
    Task<string[]> GetRawMempool(CancellationToken? token = null);
    IRpcClient[] GetRpcClients();

  }
}
