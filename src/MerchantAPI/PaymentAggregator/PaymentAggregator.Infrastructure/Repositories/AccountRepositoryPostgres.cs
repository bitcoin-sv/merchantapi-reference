// Copyright (c) 2020 Bitcoin Association

using Dapper;
using MerchantAPI.Common;
using MerchantAPI.PaymentAggregator.Domain.Models;
using MerchantAPI.PaymentAggregator.Domain.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace MerchantAPI.PaymentAggregator.Infrastructure.Repositories
{
  public class AccountRepositoryPostgres : IAccountRepository
  {
    private readonly string connectionString;

    public AccountRepositoryPostgres(IConfiguration configuration)
    {
      connectionString = configuration["PaymentAggregatorConnectionStrings:DBConnectionString"];
    }

    private NpgsqlConnection GetDbConnection()
    {
      var connection = new NpgsqlConnection(connectionString);
      RetryUtils.Exec(() => connection.Open());

      return connection;
    }

    public static void EmptyRepository(string connectionString)
    {
      using var connection = new NpgsqlConnection(connectionString);
      RetryUtils.Exec(() => connection.Open());
      string cmdText =
        "TRUNCATE ServiceRequest, Subscription, Account; ALTER SEQUENCE account_accountid_seq RESTART WITH 1; ALTER SEQUENCE subscription_subscriptionid_seq RESTART WITH 1";
      connection.Execute(cmdText, null);
    }

    public async Task<Account> AddAccountAsync(Account account)
    {
      using var connection = GetDbConnection();
      using var transaction = connection.BeginTransaction();
      string cmdText = @"
INSERT INTO Account
(contactFirstName, contactLastName, email, identity, organisationName, createdAt, identityProvider)
VALUES (@ContactFirstName, @ContactLastName, @Email, @Identity, @OrganisationName, @createdAt, @identityProvider)
ON CONFLICT DO NOTHING
RETURNING *;
";
      var dbAccount = await transaction.Connection.QueryFirstOrDefaultAsync<Account>(cmdText, 
        new
        {
          account.ContactFirstName,
          account.ContactLastName,
          account.Email,
          account.Identity,
          account.OrganisationName,
          createdAt = DateTime.UtcNow,
          account.IdentityProvider
        });
      await transaction.CommitAsync();

      return dbAccount;
    }

    public async Task UpdateAccountAsync(Account account)
    {
      using var connection = GetDbConnection();
      using var transaction = connection.BeginTransaction();
      string cmdText = @"
UPDATE Account
SET organisationName = @OrganisationName, contactFirstName = @ContactFirstName, contactLastName = @ContactLastName, email = @Email, identity = @Identity,
    identityProvider = @identityProvider
WHERE accountId = @AccountId;
";
      await transaction.Connection.ExecuteAsync(cmdText, 
        new
        {
          account.AccountId,
          account.ContactFirstName,
          account.ContactLastName,
          account.Email,
          account.Identity,
          account.OrganisationName,
          account.IdentityProvider
        });
      await transaction.CommitAsync();
    }

    public async Task<Account> GetAccountAsync(int accountId)
    {
      using var connection = GetDbConnection();
      string cmdText = @"
SELECT accountID, organisationName, contactFirstName, contactLastName, email, identity, createdAt, identityProvider
FROM Account
WHERE accountId = @AccountId;
";
      var foundAcount = await connection.QueryFirstOrDefaultAsync<Account>(cmdText, new { accountId });
      return foundAcount;
    }

    public async Task<Account[]> GetAccountsAsync()
    {
      using var connection = GetDbConnection();
      string cmdText = @"
SELECT accountID, organisationName, contactFirstName, contactLastName, email, identity, createdAt, identityProvider
FROM Account;
";
      var foundAcount = await connection.QueryAsync<Account>(cmdText);
      return foundAcount.ToArray();
    }

    public async Task<Account> GetAccountByIdentityAsync(string identity, string identityProvider)
    {
      using var connection = GetDbConnection();
      string cmdText = @"
SELECT accountID, organisationName, contactFirstName, contactLastName, email, identity, createdAt, identityProvider
FROM Account
WHERE identity = @identity AND identityProvider = @identityProvider;
";
      var foundAcount = await connection.QueryFirstOrDefaultAsync<Account>(cmdText, new { identity, identityProvider });
      return foundAcount;
    }

  }
}
