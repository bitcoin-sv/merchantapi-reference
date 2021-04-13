// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using NBitcoin;

namespace MerchantAPI.APIGateway.Domain.Models
{
  public class TxWithInput
  {
    public long TxInternalId { get; set; }
    public byte[] TxExternalId { get; set; }
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

      return new uint256(this.TxExternalId, true) == new uint256(((TxWithInput)obj).TxExternalId, true);
    }

    public override int GetHashCode()
    {
      return new uint256(TxExternalId, true).GetHashCode();
    }
  }
}
