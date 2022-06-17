// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

namespace MerchantAPI.APIGateway.Domain.Models
{
  public class PrevTxOutput
  {
    public PrevTxOutput() { }

    //public long TxInternalId { get; set; } exists on db, but there is no need to save to cache for now

    public byte[] TxExternalId { get; set; }

    public long N { get; set; }
  }
}
