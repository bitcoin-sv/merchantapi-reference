// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;

namespace MerchantAPI.APIGateway.Domain.Models
{
  public class TxOutsResponse
  {
    public DateTime Timestamp { get; set; }

    public string ReturnResult { get; set; }

    public string ResultDescription { get; set; }

    public string MinerID { get; set; }

    public TxOutResponse[] TxOuts { get; set; }
  }
}
