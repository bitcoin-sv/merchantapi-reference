// Copyright (c) 2020 Bitcoin Association

using Dapper;
using MerchantAPI.Common;
using MerchantAPI.Common.Clock;
using MerchantAPI.Common.Domain.Models;
using MerchantAPI.PaymentAggregator.Consts;
using MerchantAPI.PaymentAggregator.Domain;
using MerchantAPI.PaymentAggregator.Domain.Models;
using MerchantAPI.PaymentAggregator.Domain.Repositories;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MerchantAPI.PaymentAggregator.Infrastructure.Repositories
{
  public class ServiceLevelRepositoryPostgres : IServiceLevelRepository
  {
    private readonly string connectionString;
    private readonly IClock clock;
    // cache contains only active serviceLevels
    private static readonly Dictionary<int, ServiceLevel> cache = new Dictionary<int, ServiceLevel>();

    public ServiceLevelRepositoryPostgres(IConfiguration configuration, IClock clock)
    {
      this.connectionString = configuration["PaymentAggregatorConnectionStrings:DBConnectionString"];
      this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    private void EnsureCache()
    {
      lock (cache)
      {
        if (!cache.Any())
        {
          foreach (var serviceLevel in GetServiceLevelDbAsync().Result)
          {
            cache.Add(serviceLevel.Level, serviceLevel);
          }
        }
      }
    }


    public IEnumerable<ServiceLevel> GetServiceLevels()
    {
      EnsureCache();

      lock (cache)
      {
        return cache.Values.ToArray();
      }
    }

    private async Task<ServiceLevel[]> GetServiceLevelDbAsync()
    {
      using var connection = new NpgsqlConnection(connectionString);
      RetryUtils.Exec(() => connection.Open());

      string cmdText = @"
      SELECT * FROM ServiceLevel serviceLevel
      LEFT JOIN ServiceLevelFee fee ON serviceLevel.serviceLevelId=fee.serviceLevelId
      LEFT JOIN ServiceLevelFeeAmount feeAmount ON fee.id=feeAmount.serviceLevelFeeId 
      WHERE serviceLevel.validTo IS NULL
      ORDER BY serviceLevel.level;";
      var all = new Dictionary<long, ServiceLevel>();
      var allFees = new Dictionary<long, List<Fee>>();

      // we have to map db results to objects: 
      // for every level service that has two fees (and each fee has two feeamounts), we get 4 rows
      await connection.QueryAsync<ServiceLevel, Fee, FeeAmount, ServiceLevel>(cmdText,

            (serviceLevel, fee, feeAmount) =>
            {
              if (!all.TryGetValue(serviceLevel.ServiceLevelId, out ServiceLevel fEntity))
              {
                all.Add(serviceLevel.ServiceLevelId, fEntity = serviceLevel);
              }
              if (fee != null)
              {
                if (!allFees.ContainsKey(serviceLevel.ServiceLevelId))
                {
                  allFees.Add(serviceLevel.ServiceLevelId, new List<Fee>());
                }
                var feeT = allFees[serviceLevel.ServiceLevelId].FirstOrDefault(x => x.Id == fee.Id);
                if (feeT == null)
                {
                  fee.SetFeeAmount(feeAmount);
                  allFees[serviceLevel.ServiceLevelId].Add(fee);
                }
                else
                {
                  allFees[serviceLevel.ServiceLevelId].First(x => x.Id == fee.Id).SetFeeAmount(feeAmount);
                }
              }
              else
              {
                fEntity.Fees = null;
              }
              return fEntity;
            }
          );

      foreach (var (k, _) in allFees)
      {
        all[k].Fees = allFees[k].ToArray();
      }
      var serviceLevels = all.Values;
      return serviceLevels.ToArray();
    }

    public async Task<ServiceLevel[]> InsertServiceLevelsAsync(ServiceLevel[] serviceLevels)
    {
      using var connection = new NpgsqlConnection(connectionString);
      RetryUtils.Exec(() => connection.Open());
      using NpgsqlTransaction transaction = connection.BeginTransaction();

      // first archive currently active ServiceLevels
      await ArchiveServiceLevelsDbAsync(connection, transaction);

      // then we insert new ones
      var result = new List<ServiceLevel>();
      foreach(var serviceLevel in serviceLevels)
      {
        var feeRes = await InsertServiceLevelDbAsync(connection, serviceLevel);
        result.Add(feeRes);
      }

      await transaction.CommitAsync();

      lock (cache)
      {
        cache.Clear();
      }

      return result.ToArray();
    }

    private async Task<int> ArchiveServiceLevelsDbAsync(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
      string update =
      "UPDATE ServiceLevel " +
      "  SET validTo=@validTo " +
      "  WHERE validTo IS NULL;";

      int recordAffected = await connection.ExecuteAsync(update,
        new
        {
          validTo = clock.UtcNow()
        },
        transaction
      );

      return recordAffected;
    }

    private async Task<ServiceLevel> InsertServiceLevelDbAsync(NpgsqlConnection connection, ServiceLevel serviceLevel)
    {
      string insertFeeQuote =
            "INSERT INTO ServiceLevel (level, description) " +
            "VALUES(@level, @description) " +
            "RETURNING *;";

      var serviceLevelRes = (await connection.QueryAsync<ServiceLevel>(insertFeeQuote,
          new
          {
            level = serviceLevel.Level,
            description = serviceLevel.Description,
          })
        ).Single();
 
      if (serviceLevel.Fees == null) 
      {
        serviceLevelRes.Fees = null;
      } 
      else
      {
        List<Fee> feeResArr = new List<Fee>();
        foreach (var fee in serviceLevel.Fees)
        {
          string insertFee =
          "INSERT INTO ServiceLevelFee (serviceLevelId, feeType) " +
          "VALUES(@serviceLevelId, @feeType) " +
          "RETURNING *;";

          var feeRes = (await connection.QueryAsync<Fee>(insertFee,
              new
              {
                serviceLevelId = serviceLevelRes.ServiceLevelId,
                feeType = fee.FeeType,
              })
            ).Single();

          string insertFeeAmount =
          "INSERT INTO ServiceLevelFeeAmount (serviceLevelFeeId, satoshis, bytes, feeAmountType) " +
          "VALUES(@serviceLevelFeeId, @satoshis, @bytes, @feeAmountType) " +
          "RETURNING *;";

          var feeAmountMiningFeeRes = (await connection.QueryAsync<FeeAmount>(insertFeeAmount,
              new
              {
                serviceLevelFeeId = feeRes.Id,
                satoshis = fee.MiningFee.Satoshis,
                bytes = fee.MiningFee.Bytes,
                feeAmountType = Const.AmountType.MiningFee
              })
            ).Single();
          var feeAmountRelayFeeRes = (await connection.QueryAsync<FeeAmount>(insertFeeAmount,
              new
              {
                serviceLevelFeeId = feeRes.Id,
                satoshis = fee.RelayFee.Satoshis,
                bytes = fee.RelayFee.Bytes,
                feeAmountType = Const.AmountType.RelayFee
              })
            ).Single();

          feeRes.MiningFee = feeAmountMiningFeeRes;
          feeRes.RelayFee = feeAmountRelayFeeRes;
          feeResArr.Add(feeRes);

        }
        serviceLevelRes.Fees = feeResArr.ToArray();
      }

      return serviceLevelRes;
    }

    public static void EmptyRepository(string connectionString)
    {
      using var connection = new NpgsqlConnection(connectionString);
      RetryUtils.Exec(() => connection.Open());
      string cmdText =
        "TRUNCATE ServiceLevelFeeAmount, ServiceLevelFee, ServiceLevel;"+
        "ALTER SEQUENCE servicelevel_servicelevelid_seq RESTART WITH 1;";
      connection.Execute(cmdText, null);

      lock (cache)
      {
        cache.Clear();
      }
    }
  }
}
