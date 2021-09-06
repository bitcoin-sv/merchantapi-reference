// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using NBitcoin;
using System;

namespace MerchantAPI.APIGateway.Domain.Models
{
  public class TxWithInput
  {
    public long TxInternalId { get; set; }
    public uint256 TxExternalId { get; set; }

    public byte[] TxExternalIdBytes
    {
      get => TxExternalId.ToBytes();
      set
      {
        TxExternalId = new uint256(value);
      }
    }
    public string CallbackUrl { get; set; }
    public string CallbackToken { get; set; }
    public string CallbackEncryption { get; set; }
    public long N { get; set; }
    public byte[] PrevTxId { get; set; }
    public long Prev_N { get; set; }
    public bool DsCheck { get; set; }

    public override bool Equals(object obj)
    {
      if (obj == null)            return false;
      if (!(obj is TxWithInput))  return false;

      return (TxExternalId, N) == (((TxWithInput)obj).TxExternalId, N);
    }

    public override int GetHashCode()
    {
      return HashCode.Combine(TxExternalId, N);
    }
  }
}
