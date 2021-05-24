// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using NBitcoin;
using System;
using System.Linq;
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
      TxExternalIdBytes = txWithInput.TxExternalIdBytes;
      CallbackToken = txWithInput.CallbackToken;
      CallbackUrl = txWithInput.CallbackUrl;
      CallbackEncryption = txWithInput.CallbackEncryption;
      DSCheck = txWithInput.DsCheck;
      TxIn = new List<TxInput>();
      TxIn.Add(new TxInput(txWithInput));
    }

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

    public byte[] TxPayload { get; set; }

    public DateTime ReceivedAt { get; set; }

    public string CallbackUrl { get; set; }

    public string CallbackToken { get; set; }

    public string CallbackEncryption { get; set; }

    public bool MerkleProof { get; set; }

    public string MerkleFormat { get; set; }

    public bool DSCheck { get; set; }

    public IList<TxInput> TxIn { get; set; }

    public override int GetHashCode()
    {
      return TxExternalId.GetHashCode();
    }

    public TxInput[] OrderderInputs
    {
      get => TxIn.OrderBy(x => x.N).ToArray();
    }
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

      return x.TxExternalId == y.TxExternalId;
    }

    public int GetHashCode([DisallowNull] Tx obj)
    {
      return obj.TxExternalId.GetHashCode();
    }
  }
}
