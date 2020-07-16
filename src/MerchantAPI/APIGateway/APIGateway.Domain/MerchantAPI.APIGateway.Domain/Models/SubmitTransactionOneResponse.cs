// Copyright (c) 2020 Bitcoin Association

namespace MerchantAPI.APIGateway.Domain.Models
{
  public class SubmitTransactionOneResponse
  {
    public string Txid { get; set; }

    public string ReturnResult { get; set; }

    public string ResultDescription { get; set; }

    public SubmitTransactionConflictedTxResponse[] ConflictedWith { get; set; }

  }
}
