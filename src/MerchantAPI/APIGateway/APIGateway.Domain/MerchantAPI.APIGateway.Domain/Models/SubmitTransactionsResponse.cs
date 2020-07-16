// Copyright (c) 2020 Bitcoin Association

using System;

namespace MerchantAPI.APIGateway.Domain.Models
{
  public class SubmitTransactionsResponse
  {

    public DateTime Timestamp { get; set; }

    public string MinerId { get; set; }
    public string CurrentHighestBlockHash { get; set; }

    public long CurrentHighestBlockHeight { get; set; }

    public long TxSecondMempoolExpiry { get; set; } 

    public SubmitTransactionOneResponse[] Txs { get; set; }

    public long FailureCount { get; set; }
  }
}
