// Copyright (c) 2020 Bitcoin Association

namespace MerchantAPI.APIGateway.Domain.Models
{
  public class NotificationData
  {
    public long TxInternalId { get; set; }
    public long BlockInternalId { get; set; }
    public byte[] TxExternalId { get; set; }
    public byte[] DoubleSpendTxId { get; set; }
    public byte[] BlockHash { get; set; }
    public long BlockHeight { get; set; }
    public string CallbackUrl { get; set; }
    public string CallbackToken { get; set; }

    public string CallbackEncryption { get; set; }
    public byte[] Payload { get; set; }
  }
}
