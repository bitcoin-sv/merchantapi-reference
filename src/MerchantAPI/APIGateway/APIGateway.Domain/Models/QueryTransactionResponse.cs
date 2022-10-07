// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using MerchantAPI.Common.BitcoinRpc.Responses;

namespace MerchantAPI.APIGateway.Domain.Models
{
  public class QueryTransactionStatusResponse
  {
    public DateTime Timestamp { get; set; }

    public string Txid { get; set; }

    public string ReturnResult { get; set; }

    public string ResultDescription { get; set; }

    public string BlockHash { get; set; }

    public long? BlockHeight { get; set; }

    public long? Confirmations { get; set; }

    public string MinerID { get; set; }

    public int TxSecondMempoolExpiry { get; set; }

    public string MerkleFormat { get; set; }

    public RpcGetMerkleProof MerkleProof { get; set; }

    public RpcGetMerkleProof2 MerkleProof2{ get; set; }
  }
}
