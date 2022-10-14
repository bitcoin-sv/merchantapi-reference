// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System.Collections.Generic;

namespace MerchantAPI.APIGateway.Domain.Models
{
  public record TransactionToSubmit(string TransactionId,
                                    SubmitTransaction Transaction,
                                    bool AllowHighFees,
                                    bool DontCheckFees,
                                    bool ListUnconfirmedAncestors,
                                    PolicyQuote PolicyQuote,
                                    int TxStatus,
                                    List<string> Warnings);

}
