// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.Common.BitcoinRpc.Responses;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MerchantAPI.Common.BitcoinRpc
{
  public interface IRpcClient
  {
    public TimeSpan RequestTimeout { get; set; }
    public TimeSpan MultiRequestTimeout { get; set; }
    public int NumOfRetries { get; set; }
    public int WaitBetweenRetriesMs { get; set; }

    Task<long> GetBlockCountAsync(CancellationToken? token = null);

    Task<RpcGetBlockWithTxIds> GetBlockWithTxIdsAsync(string blockHash, CancellationToken? token = null);

    Task<RpcGetBlock> GetBlockAsync(string blockHash, int verbosity, CancellationToken? token = null);

    Task<RpcBitcoinStreamReader> GetBlockAsStreamAsync(string blockHash, CancellationToken? token = null);

    Task<byte[]> GetBlockByHeightAsBytesAsync(long blockHeight, CancellationToken? token = null);

    Task<string> GetBlockHashAsync(long height, CancellationToken? token = null);

    Task<RpcGetBlockHeader> GetBlockHeaderAsync(string blockHash, CancellationToken? token = null);

    Task<string> GetBlockHeaderAsHexAsync(string blockHash, CancellationToken? token = null);

    Task<RpcGetRawTransaction> GetRawTransactionAsync(string txId, int retryCount = 0, CancellationToken? token = null);

    Task<byte[]> GetRawTransactionAsBytesAsync(string txId, CancellationToken? token = null);

    Task<string> GetBestBlockHashAsync(CancellationToken? token = null);

    Task<string> SendRawTransactionAsync(byte[] transaction, bool allowhighfees, bool dontCheckFees, CancellationToken? token = null);

    Task<RpcSendTransactions> SendRawTransactionsAsync((byte[] transaction, bool allowhighfees, bool dontCheckFees, bool listUnconfirmedAncestors, Dictionary<string, object> config)[] transactions, CancellationToken? token = null);

    Task StopAsync(CancellationToken? token = null);

    Task<string[]> GenerateAsync(int n, CancellationToken? token = null);

    Task<string> SendToAddressAsync(string address, double amount, CancellationToken? token = null);

    Task<RpcGetBlockchainInfo> GetBlockchainInfoAsync(CancellationToken? token = null);

    Task<RpcGetMerkleProof> GetMerkleProofAsync(string txId, string blockHash, CancellationToken? token = null);

    Task<RpcGetMerkleProof2> GetMerkleProof2Async(string blockHash, string txId, CancellationToken? token = null);

    Task<RpcActiveZmqNotification[]> ActiveZmqNotificationsAsync(CancellationToken? token = null, bool retry = false);

    Task<RpcGetNetworkInfo> GetNetworkInfoAsync(CancellationToken? token = null, bool retry = false);

    Task<RpcGetTxOuts> GetTxOutsAsync(IEnumerable<(string txId, long N)> outpoints, string[] fieldList, CancellationToken? token = null);

    Task<string> SubmitBlock(byte[] block, CancellationToken? token = null);

    Task<string[]> GetRawMempool(CancellationToken? token = null); // non-verbose options currently not supported

    Task<RpcVerifyScriptResponse[]> VerifyScriptAsync(bool stopOnFirstInvalid,
                                                      int totalTimeoutSec,
                                                      IEnumerable<(string Tx, int N)> dsTx, CancellationToken? token = null);
    Task AddNodeAsync(string host, int P2PPort, CancellationToken? token = null);

    Task DisconnectNodeAsync(string host, int P2PPort, CancellationToken? token = null);

    Task<int> GetConnectionCountAsync(CancellationToken? token = null);
  }
}
