// Copyright (c) 2020 Bitcoin Association

using Dapper;
using MerchantAPI.Common;
using MerchantAPI.Common.Tasks;
using MerchantAPI.PaymentAggregator.Domain.Models;
using MerchantAPI.PaymentAggregator.Domain.Repositories;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace MerchantAPI.PaymentAggregator.Infrastructure.Repositories
{
  public class SubscriptionRepositoryPostgres : ISubscriptionRepository
  {
    private readonly string connectionString;

    public SubscriptionRepositoryPostgres(IConfiguration configuration)
    {
      connectionString = configuration["PaymentAggregatorConnectionStrings:DBConnectionString"];
    }

    private NpgsqlConnection GetDbConnection()
    {
      var connection = new NpgsqlConnection(connectionString);
      RetryUtils.Exec(() => connection.Open());

      return connection;
    }

    public async Task<Subscription> AddSubscriptionAsync(int accountId, string serviceType, DateTime validFrom)
    {
      using var connection = GetDbConnection();
      using var transaction = connection.BeginTransaction();
      string cmdText = @"
INSERT INTO Subscription
(accountId, serviceType, validFrom)
VALUES (@accountId, @serviceType, @validFrom)
ON CONFLICT DO NOTHING
RETURNING *;
";
      var dbSubscription = await transaction.Connection.QueryFirstOrDefaultAsync<Subscription>(cmdText,
        new
        {
          accountId, 
          serviceType,
          validFrom
        });
      await transaction.CommitAsync();

      return dbSubscription;
    }

    public async Task<bool> DeleteSubscriptionAsync(int accountId, int subscriptionId)
    {
      using var connection = GetDbConnection();
      using var transaction = connection.BeginTransaction();

      string cmdText = @"
UPDATE Subscription
SET validTo = @validTo
WHERE accountID = @accountID AND subscriptionId = @subscriptionId AND validTo IS NULL;
";

      var result = await transaction.Connection.ExecuteAsync(cmdText,
        new
        {
          accountId,
          subscriptionId,
          validTo = DateTime.UtcNow
        });
      await transaction.CommitAsync();

      return result > 0;
    }

    public async Task<Subscription[]> GetSubscriptionsAsync(int accountId, bool onlyActive)
    {
      using var connection = GetDbConnection();

      string cmdText = @"
SELECT subscriptionId, serviceType, validFrom, validTo
FROM Subscription
WHERE accountID = @accountID
";
      if (onlyActive)
      {
        cmdText += " AND validTo IS NULL ";
      }

      cmdText += " ORDER BY validFrom;";

      return (await connection.QueryAsync<Subscription>(cmdText, new { accountId })).ToArray();
    }

    public async Task<Subscription> GetSubscriptionAsync(int accountId, int subscriptionId)
    {
      using var connection = GetDbConnection();

      string cmdText = @"
SELECT subscriptionId, serviceType, validFrom, validTo
FROM Subscription
WHERE accountID = @accountID AND subscriptionId = @subscriptionId;
";
      return await connection.QueryFirstOrDefaultAsync<Subscription>(cmdText, new { accountId, subscriptionId });
    }
  }
}
