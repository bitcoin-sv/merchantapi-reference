// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.Common.BitcoinRpc.Responses;
using System;

namespace MerchantAPI.APIGateway.Domain.Models
{
  public class NotificationData
  {
    public string NotificationType { get; set; }
    public long TxInternalId { get; set; }
    public long BlockInternalId { get; set; }
    public byte[] TxExternalId { get; set; }
    public byte[] DoubleSpendTxId { get; set; }
    public byte[] Payload { get; set; }
    public RpcGetMerkleProof MerkleProof { get; set; }
    public byte[] BlockHash { get; set; }
    public long BlockHeight { get; set; }

    public string CallbackUrl { get; set; }
    public string CallbackToken { get; set; }
    public string CallbackEncryption { get; set; }

    public DateTime CreatedAt { get; set; }
    public int ErrorCount { get; set; }
  }
}
