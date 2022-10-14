// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

namespace MerchantAPI.APIGateway.Domain.Models
{
  public class TxOutCollidedWith
  {
    public string TxId { get; set; }

    public long Size { get; set; }

    public string Hex { get; set; }
  }
}
