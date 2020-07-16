// Copyright (c) 2020 Bitcoin Association

using NBitcoin;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

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
  }

  public class TxWithInputComparer : IEqualityComparer<TxWithInput>
  {
    public bool Equals([AllowNull] TxWithInput x, [AllowNull] TxWithInput y)
    {
      if (x == null || y == null)
      {
        return false;
      }

      return new uint256(x.TxExternalId, true) == new uint256(y.TxExternalId, true);
    }

    public int GetHashCode([DisallowNull] TxWithInput obj)
    {
      return new uint256(obj.TxExternalId, true).GetHashCode();
    }
  }

}
