// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

namespace MerchantAPI.APIGateway.Domain.Models
{
  public class SubmitTransactionConflictedTxResponse
  {
    public string Txid { get; set; }

    public long Size { get; set; }

    public string Hex { get; set; }
  }
}
