// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;

namespace MerchantAPI.APIGateway.Domain.Models
{
  public class TxBlockDoubleSpend
  {
    public long TxInternalId { get; set; }
    public long BlockInternalId { get; set; }
    public byte[] DsTxId { get; set; }
    public byte[] DsTxPayload { get; set; }
    public DateTime? SentDsNotificationAt { get; set; }
  }
}
