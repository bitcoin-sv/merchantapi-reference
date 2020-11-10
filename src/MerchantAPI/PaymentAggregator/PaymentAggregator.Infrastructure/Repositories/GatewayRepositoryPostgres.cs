// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.PaymentAggregator.Domain.Models;
using MerchantAPI.PaymentAggregator.Domain.Repositories;
using Dapper;
using Npgsql;
using System;
using System.Collections.Generic;
using MerchantAPI.Common;
using System.Linq;
using Microsoft.Extensions.Configuration;
using MerchantAPI.Common.Clock;

namespace MerchantAPI.PaymentAggregator.Infrastructure.Repositories
{
  public class GatewayRepositoryPostgres : IGatewayRepository
  {

    readonly string connectionString;
    private readonly IClock clock;
    // cache contains only active/disabled gateways, so that mAPI calls get results faster
    private static readonly Dictionary<int, Gateway> cache = new Dictionary<int, Gateway>();

    public GatewayRepositoryPostgres(IConfiguration configuration, IClock clock)
    {
      connectionString = configuration["PaymentAggregatorConnectionStrings:DBConnectionString"];
      this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    private void EnsureCache()
    {
      lock (cache)
      {
        if (!cache.Any())
        {
          foreach (var gateway in GetGatewaysDb())
          {
            cache.Add(gateway.Id, gateway);
          }
        }
      }
    }

    private string GetCachedUrl(Gateway gateway)
    {
      return $"{gateway.Url.ToLower()}";
    }


    public Gateway CreateGateway(Gateway gateway)
    {
      EnsureCache();
      lock (cache) 
      {
        if (cache.Values.Any( x => x.Url == GetCachedUrl(gateway)))
        {
          return null;
        }
        var createdGateway = CreateGatewayDb(gateway);
        if (createdGateway != null)
        {
          cache.Add(createdGateway.Id, createdGateway);
        }
        return createdGateway;
      }
    }

    private Gateway CreateGatewayDb(Gateway Gateway)
    {
      using var connection = new NpgsqlConnection(connectionString);
      RetryUtils.Exec(() => connection.Open());
      using var transaction = connection.BeginTransaction();

      string insertOrUpdate =
        "INSERT INTO Gateway " +
        "  (url, minerRef, email, organisationName, contactFirstName, contactLastName, createdAt, remarks, disabledAt) " +
        "  VALUES (@url, @minerRef, @email, @organisationName, @contactFirstName, @contactLastName, @createdAt, @remarks, @disabledAt)" +
        "  ON CONFLICT DO NOTHING" +
        "  RETURNING *"
      ;
      var now = clock.UtcNow();

      var insertedGateway = connection.Query<Gateway>(insertOrUpdate,
        new
        {
          gatewayId = Gateway.Id,
          url = Gateway.Url,
          minerRef = Gateway.MinerRef,
          email = Gateway.Email,
          organisationName = Gateway.OrganisationName,
          contactFirstName = Gateway.ContactFirstName,
          contactLastName = Gateway.ContactLastName,
          createdAt = Gateway.CreatedAt,
          remarks = Gateway.Remarks,
          disabledAt = Gateway.DisabledAt
        },
        transaction
      ).SingleOrDefault();
      transaction.Commit();

      return insertedGateway;
    }


    public bool UpdateGateway(Gateway gateway)
    {
      return UpdateGateway(gateway, UpdateGatewayDb);
    }

    private bool UpdateGateway(Gateway gateway, Func<Gateway, (Gateway, bool)> func)
    {
      EnsureCache();
      lock (cache)
      {
        var cachedKey = gateway.Id;
        if (!cache.ContainsKey(cachedKey))
        {
          return false;
        }
        (Gateway updatedGateway, bool success) = func(gateway);
        if (success)
        {
          cache[cachedKey] = updatedGateway;
        }
        return success;
      }
    }

    private (Gateway, bool) UpdateGatewayDb(Gateway gateway)
    {
      using var connection = new NpgsqlConnection(connectionString);
      RetryUtils.Exec(() => connection.Open());
      using var transaction = connection.BeginTransaction();
      string update =
      "UPDATE Gateway " +
      "  SET  url=@url, minerRef=@minerRef, email=@email, organisationName=@organisationName, contactFirstName=@contactFirstName, contactLastName=@contactLastName, remarks=@remarks, disabledAt=@disabledAt " +
      "  WHERE gatewayId=@gatewayId " +
      "  RETURNING *";

      Gateway updatedGateway = connection.Query<Gateway>(update,
        new
        {
          gatewayId = gateway.Id,
          url = gateway.Url.ToLower(),
          minerRef = gateway.MinerRef,
          email = gateway.Email,
          organisationName = gateway.OrganisationName,
          contactFirstName = gateway.ContactFirstName,
          contactLastName = gateway.ContactLastName,
          remarks = gateway.Remarks,
          disabledAt = gateway.DisabledAt
        },
        transaction
      ).SingleOrDefault();
      transaction.Commit();

      return (updatedGateway, updatedGateway != null);
    }

    public bool UpdateGatewayError(Gateway gateway)
    {
      return UpdateGateway(gateway, UpdateGatewayErrorDb);
    }

    private (Gateway, bool) UpdateGatewayErrorDb(Gateway gateway)
    {
      using var connection = new NpgsqlConnection(connectionString);
      RetryUtils.Exec(() => connection.Open());

      using var transaction = connection.BeginTransaction();
      string update =
      "UPDATE Gateway " +
      "  SET  lastError=@lastError, lastErrorAt=@lastErrorAt " +
      "  WHERE gatewayId=@gatewayId" +
      "  RETURNING *";

      Gateway updatedGateway = connection.Query<Gateway>(update,
        new
        {
          lastError = gateway.LastError,
          lastErrorAt = gateway.LastErrorAt,
          gatewayId = gateway.Id
        },
        transaction
      ).SingleOrDefault();
      transaction.Commit();

      return (updatedGateway, updatedGateway != null);
    }


    public Gateway GetGateway(int gatewayId)
    {
      EnsureCache();
      lock (cache) 
      {
        var cachedKey = gatewayId;
        if (!cache.ContainsKey(cachedKey))
        {
          return null;
        }
        cache.TryGetValue(cachedKey, out Gateway result);
        return result;
      }
    }

    public int DeleteGateway(int gatewayId)
    {
      EnsureCache();
      lock (cache) 
        {
        var cachedKey = gatewayId;
        if (!cache.ContainsKey(cachedKey))
        {
          return 0;
        }
        var deleted = DeleteGatewayDb(cachedKey);
        if (deleted > 0)
        {
          cache.Remove(cachedKey);
        }
        return deleted;
      }
    }

    private int DeleteGatewayDb(int gatewayId)
    {
      using var connection = new NpgsqlConnection(connectionString);

      RetryUtils.Exec(() => connection.Open());
      using var transaction = connection.BeginTransaction();
      string update =
      "UPDATE Gateway " +
      "  SET  deletedAt=@deletedAt " +
      "  WHERE gatewayId=@gatewayId"; 

      int recordAffected = connection.Execute(update,
        new
        {
          gatewayId,
          deletedAt = clock.UtcNow()
        },
        transaction
      );
      transaction.Commit();

      return recordAffected;
    }

    public IEnumerable<Gateway> GetGateways(bool onlyActive=false)
    {
      EnsureCache();

      lock (cache)
      {
        if (onlyActive)
        {
          return cache.Values.ToArray().Where(x => x.DisabledAt == null || x.DisabledAt > clock.UtcNow());
        }
        else
        {
          return cache.Values.ToArray();
        }
      }
    }

    private IEnumerable<Gateway> GetGatewaysDb()
    {
      using var connection = new NpgsqlConnection(connectionString);
      RetryUtils.Exec(() => connection.Open());
      using var transaction = connection.BeginTransaction();

      string cmdText =
        @"SELECT gatewayId, url, minerRef, email, organisationName, contactFirstName, contactLastName, createdAt, remarks, lastError, lastErrorAt, disabledAt, deletedAt FROM Gateway WHERE deletedAt IS NULL ORDER by gatewayId";
      return connection.Query<Gateway>(cmdText, null, transaction);
    }

    public static void EmptyRepository(string connectionString)
    {
      using var connection = new NpgsqlConnection(connectionString);
      RetryUtils.Exec(() => connection.Open());
      string cmdText =
        "TRUNCATE gateway; ALTER SEQUENCE gateway_gatewayid_seq RESTART WITH 1;";
      connection.Execute(cmdText, null);

      lock (cache) 
      {
        cache.Clear();
      }
    }
  }
}
