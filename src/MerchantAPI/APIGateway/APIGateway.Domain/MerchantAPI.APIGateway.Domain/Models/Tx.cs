// Copyright (c) 2020 Bitcoin Association

using NBitcoin;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace MerchantAPI.APIGateway.Domain.Models
{
  public class Tx
  {
    public Tx() 
    {
      TxIn = new List<TxInput>();
    }
    public Tx(TxWithInput txWithInput)
    {
      TxInternalId = txWithInput.TxInternalId;
      TxExternalId = txWithInput.TxExternalId;
      CallbackToken = txWithInput.CallbackToken;
      CallbackUrl = txWithInput.CallbackUrl;
      CallbackEncryption = txWithInput.CallbackEncryption;
      TxIn = new List<TxInput>();
      TxIn.Add(new TxInput(txWithInput));
    }

    public long TxInternalId { get; set; }

    public byte[] TxExternalId { get; set; }

    public byte[] TxPayload { get; set; }

    public DateTime ReceivedAt { get; set; }

    public string CallbackUrl { get; set; }

    public string CallbackToken { get; set; }

    public string CallbackEncryption { get; set; }

    public bool MerkleProof { get; set; }

    public bool DSCheck { get; set; }

    public IList<TxInput> TxIn { get; set; }
  }

  public class TxInput
  {
    public TxInput() { }
    public TxInput(TxWithInput txWithInput)
    {
      TxInternalId = txWithInput.TxInternalId;
      N = txWithInput.N;
      PrevTxId = txWithInput.PrevTxId;
      PrevN = txWithInput.Prev_N;
    }

    public long TxInternalId { get; set; }

    public long N { get; set; }

    public byte[] PrevTxId { get; set; }

    public long PrevN { get; set; }
  }

  public class TxComparer : IEqualityComparer<Tx>
  {
    public bool Equals([AllowNull] Tx x, [AllowNull] Tx y)
    {
      if (x == null || y == null)
      {
        return false;
      }

      return new uint256(x.TxExternalId, true) == new uint256(y.TxExternalId, true);
    }

    public int GetHashCode([DisallowNull] Tx obj)
    {
      return new uint256(obj.TxExternalId, true).GetHashCode();
    }
  }
}
