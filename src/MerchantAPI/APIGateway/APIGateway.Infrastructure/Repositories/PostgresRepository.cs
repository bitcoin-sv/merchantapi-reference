// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSEusing MerchantAPI.APIGateway.Domain;

using MerchantAPI.APIGateway.Domain;
using MerchantAPI.Common.Clock;
using MerchantAPI.Common.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Npgsql;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Infrastructure.Repositories
{
  public class PostgresRepository
  {
    protected readonly IClock clock;
    readonly string connectionString;
    readonly TimeSpan openConnectionTimeout;
    readonly int openConnectionRetries;

    public PostgresRepository(IOptions<AppSettings> appSettings, IConfiguration configuration, IClock clock)
    {
      connectionString = configuration["ConnectionStrings:DBConnectionString"];
      this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
      openConnectionTimeout = TimeSpan.FromSeconds(appSettings.Value.DbConnection.OpenConnectionTimeoutSec.Value);
      openConnectionRetries = appSettings.Value.DbConnection.OpenConnectionMaxRetries.Value;
    }

    protected async Task<NpgsqlConnection> GetDbConnectionAsync()
    {
      var connection = new NpgsqlConnection(connectionString);

      using CancellationTokenSource cts = new(openConnectionTimeout);
      await RetryUtils.ExecAsync(() => connection.OpenAsync(cts.Token), retry: openConnectionRetries);

      return connection;
    }
  }
}
