// Copyright (c) 2020 Bitcoin Association

using System.Collections.Generic;
using System.Threading.Tasks;
using MerchantAPI.APIGateway.Domain.Models;

namespace MerchantAPI.APIGateway.Domain.Actions
{
  public interface IMapi 
  {
    Task<SubmitTransactionResponse> SubmitTransactionAsync(SubmitTransaction request, UserAndIssuer user);
    Task<SubmitTransactionsResponse> SubmitTransactionsAsync(IEnumerable<SubmitTransaction> request, UserAndIssuer user);
    Task<QueryTransactionStatusResponse> QueryTransaction(string id);
  }
}
