// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

namespace MerchantAPI.APIGateway.Domain.Models
{
  public class TxOutResponse
  {
    public string Error { get; set; }

    public TxOutCollidedWith CollidedWith { get; set; }

    public string ScriptPubKey { get; set; }

    public long? ScriptPubKeyLen { get; set; }

    public decimal? Value { get; set; }

    public bool? IsStandard { get; set; }

    public long? Confirmations { get; set; }

  }
}
