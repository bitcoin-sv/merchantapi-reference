// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.APIGateway.Domain.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Domain.Repositories
{
  public interface IFeeQuoteRepository
  {
    IEnumerable<FeeQuote> GetCurrentFeeQuotes();
    FeeQuote GetCurrentFeeQuoteByIdentity(UserAndIssuer identity);
    FeeQuote GetFeeQuoteById(long feeQuoteId);
    IEnumerable<FeeQuote> GetValidFeeQuotes();
    IEnumerable<FeeQuote> GetValidFeeQuotesByIdentity(UserAndIssuer identity);
    IEnumerable<FeeQuote> GetFeeQuotes();
    IEnumerable<FeeQuote> GetFeeQuotesByIdentity(UserAndIssuer identity);
    Task<FeeQuote> InsertFeeQuoteAsync(FeeQuote feeQuote);

  }

}
