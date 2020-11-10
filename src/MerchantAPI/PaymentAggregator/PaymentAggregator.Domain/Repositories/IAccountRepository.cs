// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.PaymentAggregator.Domain.Models;
using System.Threading.Tasks;

namespace MerchantAPI.PaymentAggregator.Domain.Repositories
{
  public interface IAccountRepository
  {
    Task<Account> AddAccountAsync(Account account);
    Task UpdateAccountAsync(Account account);
    Task<Account> GetAccountAsync(int accountId);
    Task<Account> GetAccountByIdentityAsync(string identity, string identityProvider);
    Task<Account[]> GetAccountsAsync();
  }
}
