// Copyright (c) 2020 Bitcoin Association

using Dapper;
using MerchantAPI.Common;
using MerchantAPI.Common.Clock;
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
  public class ServiceRequestRepositoryPostgres : IServiceRequestRepository
  {
    private readonly string connectionString;
    private readonly IClock clock;

    public ServiceRequestRepositoryPostgres(IConfiguration configuration, IClock clock)
    {
      this.connectionString = configuration["PaymentAggregatorConnectionStrings:DBConnectionString"];
      this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<ServiceRequest[]> GetServiceRequestsAsync()
    {
      var connection = new NpgsqlConnection(connectionString);
      RetryUtils.Exec(() => connection.Open());
      string cmdText = @"
      SELECT serviceRequestId, subscriptionId, created, responseCode, executionTimeMs
      FROM ServiceRequest;
      ";
      var serviceRequests = await connection.QueryAsync<ServiceRequest>(cmdText);
      return serviceRequests.ToArray();
    }

    public async Task<ServiceRequest> InsertServiceRequestAsync(ServiceRequest serviceRequest)
    {
      var connection = new NpgsqlConnection(connectionString);
      RetryUtils.Exec(() => connection.Open());
      string insertServiceRequest =
            "INSERT INTO ServiceRequest (subscriptionId, created, responseCode, executionTimeMs) " +
            "VALUES(@subscriptionId, @created, @responseCode, @executionTimeMs) " +
            "RETURNING *;";

      var serviceLevelRes = (await connection.QueryAsync<ServiceRequest>(insertServiceRequest,
          new
          {
            subscriptionId = serviceRequest.SubscriptionId,
            created = clock.UtcNow(),
            responseCode = serviceRequest.ResponseCode,
            executionTimeMs = serviceRequest.ExecutionTimeMs
          })
        ).Single();
      return serviceLevelRes;
    }

    public async Task CleanUpServiceRequestAsync(DateTime createdBefore)
    {
      var connection = new NpgsqlConnection(connectionString);
      RetryUtils.Exec(() => connection.Open());
      using var transaction = await connection.BeginTransactionAsync();

      await transaction.Connection.ExecuteAsync(
        @"DELETE FROM ServiceRequest WHERE created < @createdBefore;", new { createdBefore });

      await transaction.CommitAsync();
    }

    public static void EmptyRepository(string connectionString)
    {
      using var connection = new NpgsqlConnection(connectionString);
      RetryUtils.Exec(() => connection.Open());
      string cmdText =
        "TRUNCATE ServiceRequest;";
      connection.Execute(cmdText);
    }
  }
}
