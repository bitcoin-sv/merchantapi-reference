// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System.Collections.Generic;
using System.Threading.Tasks;
using MerchantAPI.APIGateway.Domain.Models;
using MerchantAPI.Common.Authentication;

namespace MerchantAPI.APIGateway.Domain.Actions
{
  public interface IMapi 
  {
    Task<SubmitTransactionResponse> SubmitTransactionAsync(SubmitTransaction request, UserAndIssuer user);
    Task<SubmitTransactionsResponse> SubmitTransactionsAsync(IEnumerable<SubmitTransaction> request, UserAndIssuer user);
    Task<QueryTransactionStatusResponse> QueryTransaction(string id);
  }
}
