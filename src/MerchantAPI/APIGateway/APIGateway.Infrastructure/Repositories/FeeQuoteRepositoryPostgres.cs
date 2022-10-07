// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using Dapper;
using MerchantAPI.APIGateway.Domain;
using MerchantAPI.APIGateway.Domain.Models;
using MerchantAPI.APIGateway.Domain.Repositories;
using MerchantAPI.Common.Authentication;
using MerchantAPI.Common.Clock;
using MerchantAPI.Common.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Infrastructure.Repositories
{
  public class FeeQuoteRepositoryPostgres : PostgresRepository, IFeeQuoteRepository
  {
    private readonly double quoteExpiryMinutes;
    // cache contains valid and future feeQuotes, so that mAPI calls get results faster
    private static readonly Dictionary<(string Identity, string IdentityProvider), List<FeeQuote>> cache = new();

    public FeeQuoteRepositoryPostgres(IOptions<AppSettings> appSettings, IConfiguration configuration, IClock clock)
           : base(appSettings, configuration, clock)
    {
      this.quoteExpiryMinutes = appSettings.Value.QuoteExpiryMinutes.Value;
    }

    private void EnsureCache()
    {
      if (!cache.Any())
      {
        foreach (var feeQuote in GetAllFeeQuotes(null, true))
        {
          var cachedKey = GetCacheKey(feeQuote);
          var feeQuotes = cache.GetValueOrDefault(cachedKey, new List<FeeQuote>());
          feeQuotes.Add(feeQuote);
          cache[cachedKey] = feeQuotes;
        }
      }    
    }


    private static (string identity, string identityProvider) GetCacheKey(FeeQuote feeQuote)
    {
      return GetCacheKey(feeQuote.Identity, feeQuote.IdentityProvider);
    }

    private static (string identity, string identityProvider) GetCacheKey(UserAndIssuer identity)
    {
      return GetCacheKey(identity?.Identity, identity?.IdentityProvider);
    }
    private static (string identity, string identityProvider) GetCacheKey(string identity, string identityProvider)
    {
      return (identity ?? "", identityProvider ?? ""); // ("", "") = key for anonymous user
    }

    public FeeQuote GetCurrentFeeQuoteByIdentity(UserAndIssuer identity)
    {
      var feeQuote = GetValidFeeQuotes(identity, current: true);

      return feeQuote.SingleOrDefault();
    }

    public IEnumerable<FeeQuote> GetCurrentFeeQuotes()
    {

      return GetValidFeeQuotes(null, ignoreIdentity: true, current: true); 

    }

    public FeeQuote GetFeeQuoteById(long feeQuoteId)
    {
      return GetFeeQuoteById(feeQuoteId, null);
    }

    public FeeQuote GetFeeQuoteById(long feeQuoteId, bool? orderByFeeAmountDesc)
    {
      string selectFeeQuote = $@"
              SELECT * FROM FeeQuote feeQuote
              JOIN Fee fee ON feeQuote.id=fee.feeQuote 
              JOIN FeeAmount feeAmount ON fee.id=feeAmount.fee 
              WHERE feeQuote.id = @id
              { (!orderByFeeAmountDesc.HasValue ? "" :
              $"ORDER BY feeAmount.id { (orderByFeeAmountDesc.Value ? "DESC" : "ASC") }") }
              ;";
      return GetFeeQuotesDb(selectFeeQuote, new { id = feeQuoteId }).SingleOrDefault();
    }

    public IEnumerable<FeeQuote> GetValidFeeQuotes()
    {
      return GetValidFeeQuotes(null, true);
    }

    public IEnumerable<FeeQuote> GetFeeQuotes()
    {
      return GetAllFeeQuotes(null, ignoreIdentity: true);
    }
    public IEnumerable<FeeQuote> GetFeeQuotesByIdentity(UserAndIssuer identity)
    {
      return GetAllFeeQuotes(identity);
    }

    public IEnumerable<FeeQuote> GetValidFeeQuotesByIdentity(UserAndIssuer identity)
    {
      return GetValidFeeQuotes(identity);
    }

    private IEnumerable<FeeQuote> GetAllFeeQuotes(UserAndIssuer feeQuoteIdentity, bool ignoreIdentity = false)
    {
      string cmdText = @"
              SELECT * FROM FeeQuote feeQuote
              JOIN Fee fee ON feeQuote.id=fee.feeQuote
              JOIN FeeAmount feeAmount ON fee.id=feeAmount.fee ";
      List<string> whereParams = new();

      if (!ignoreIdentity)
      {
        if (feeQuoteIdentity?.Identity != null)
        {
          whereParams.Add("identity = @identity ");
        }
        else if (feeQuoteIdentity?.IdentityProvider != null)
        {
          whereParams.Add("identityProvider = @identityProvider ");
        }
        else
        {
          whereParams.Add("identity is NULL ");
        }
      }
      if (whereParams.Any())
      {
        cmdText += "WHERE " + String.Join("AND ", whereParams);
      }

      cmdText += "ORDER BY feeQuote.createdAt, feeQuote.validFrom;";

      var feeQuotes = GetFeeQuotesDb(cmdText, new { identity = feeQuoteIdentity?.Identity, identityProvider = feeQuoteIdentity?.IdentityProvider }).ToArray();
      return feeQuotes;
    }
    private IEnumerable<FeeQuote> GetValidFeeQuotes(UserAndIssuer feeQuoteIdentity, bool ignoreIdentity = false, bool current = false)
    {
      // in cache every value represents list of valid feeQuotes + future feequotes, that are not yet valid
      // on every call we get feeQuotes from cache and update it
      // mAPI calls should always have UserAndIssuer fully filled or null, so only one value in cache will be checked and updated

      List<FeeQuote> mergedResultFeeQuotes = new();
      lock (cache)
      {
        EnsureCache();
        // get keys from cache, that we are interested in
        var keys = new List<(string identity, string identityProvider)>();
        if (feeQuoteIdentity?.Identity != null && feeQuoteIdentity?.IdentityProvider == null)
        {
          keys = cache.Keys.Where(k => k.Identity == feeQuoteIdentity.Identity).ToList();
        }
        else if (feeQuoteIdentity?.Identity == null && feeQuoteIdentity?.IdentityProvider != null)
        {
          keys = cache.Keys.Where(k => k.IdentityProvider == feeQuoteIdentity.IdentityProvider).ToList();
        }
        else if (ignoreIdentity)
        {
          keys = cache.Keys.ToList();
        }
        else
        {
          var key = GetCacheKey(feeQuoteIdentity);
          if (cache.ContainsKey(key))
          {
            keys.Add(key);
          }
          else
          {
            return mergedResultFeeQuotes; // empty list
          }
        }

        var now = clock.UtcNow();
        foreach (var key in keys)
        {
          // refresh values in cache, based on validFrom filtering
          var feeQuotes = cache[key];

          var futureFeeQuotes = feeQuotes.Where(x => x.ValidFrom > now).ToList(); // we need feeQuotes in cache for future calls
          var resultFeeQuotes = feeQuotes.Where(x => x.ValidFrom <= now).ToList(); // we return feeQuotes that are valid now: validFrom must be now or past, createdAt can only be in past
          DateTime validFromWithExpiry = now.AddMinutes(-quoteExpiryMinutes); // we also want to get the previous valid feeQuote 
          var validFeeQuotes = resultFeeQuotes.Where(x => x.ValidFrom >= validFromWithExpiry && x.CreatedAt >= validFromWithExpiry).ToArray();
          List<FeeQuote> prevValidFeeQuotes = resultFeeQuotes
            .Select(x => x.Identity == null ? null : x.Identity + " " + x.IdentityProvider)
            .Distinct()
            .Select(code => resultFeeQuotes.Last(x => (x.Identity == null ? null : x.Identity + " " + x.IdentityProvider) == code))
            .ToList();
          if (!validFeeQuotes.Any())
          {
            validFeeQuotes = validFeeQuotes.Concat(prevValidFeeQuotes).ToArray();
          }


          cache[key] = validFeeQuotes.Concat(futureFeeQuotes).ToList();


          if (current)
          {
            mergedResultFeeQuotes.AddRange(prevValidFeeQuotes);
          }
          else
          {
            mergedResultFeeQuotes.AddRange(validFeeQuotes);
          }
        }

      }
      return mergedResultFeeQuotes;

    }

    private IEnumerable<FeeQuote> GetFeeQuotesDb(string cmdText, object parameters=null)
    {
      using var connection = GetDbConnectionAsync().Result;
      var all = new Dictionary<long, FeeQuote>();
      var allFees = new Dictionary<long, List<Fee>>();
      connection.Query<FeeQuote, Fee, FeeAmount, FeeQuote>(cmdText,

            (feeQuote, fee, feeAmount) =>
            {
              if (!all.TryGetValue(feeQuote.Id, out FeeQuote fEntity))
              {
                all.Add(feeQuote.Id, fEntity = feeQuote);
              }

              if (!allFees.ContainsKey(feeQuote.Id))
              {
                allFees.Add(feeQuote.Id, new List<Fee>());
              }
              var feeT = allFees[feeQuote.Id].FirstOrDefault(x => x.Id == fee.Id);
              if (feeT == null)
              {
                fee.SetFeeAmount(feeAmount);
                allFees[feeQuote.Id].Add(fee);
              }
              else
              {
                allFees[feeQuote.Id].First(x => x.Id == fee.Id).SetFeeAmount(feeAmount);
              }

              return fEntity;
            },
             param: parameters
          );

      foreach (var (k, _) in all) 
      {
        all[k].Fees = allFees[k].ToArray();
      }
      var feeQuotes = all.Values;
      if (!feeQuotes.Any())
      {
        return Enumerable.Empty<FeeQuote>();
      }

      return feeQuotes.ToArray();
    }


    public async Task<FeeQuote> InsertFeeQuoteAsync(FeeQuote feeQuote)
    {
      if (feeQuote.CreatedAt > feeQuote.ValidFrom)
      {
        return null;
      }
      using var connection = await GetDbConnectionAsync();
      using var transaction = await connection.BeginTransactionAsync();

      string insertFeeQuote =
            "INSERT INTO FeeQuote (createdat, validfrom, identity, identityprovider, policies) " +
            "VALUES(@createdat, @validFrom, @identity, @identityprovider, @policies) " +
            "RETURNING *;";

      var feeQuoteRes = (await connection.QuerySingleAsync<FeeQuote>(insertFeeQuote,
          new
          {
            createdat = feeQuote.CreatedAt,
            validFrom = feeQuote.ValidFrom,
            identity = feeQuote.Identity,
            identityprovider = feeQuote.IdentityProvider,
            policies = feeQuote.Policies
          })
        );

      if (feeQuoteRes == null)
        return null;

      List<Fee> feeResArr = new();
      foreach (var fee in feeQuote.Fees)
      {
        string insertFee =
        "INSERT INTO Fee (feeQuote, feeType) " +
        "VALUES(@feeQuote, @feeType) " +
        "RETURNING *;";

        var feeRes = await connection.QuerySingleAsync<Fee>(insertFee,
            new
            {
              feeQuote = feeQuoteRes.Id,
              feeType = fee.FeeType,
            });

        string insertFeeAmount =
        "INSERT INTO FeeAmount (fee, satoshis, bytes, feeAmountType) " +
        "VALUES(@fee, @satoshis, @bytes, @feeAmountType) " +
        "RETURNING *;";

        var feeAmountMiningFeeRes = await connection.QuerySingleAsync<FeeAmount>(insertFeeAmount,
            new
            {
              fee = feeRes.Id,
              satoshis = fee.MiningFee.Satoshis,
              bytes = fee.MiningFee.Bytes,
              feeAmountType = Const.AmountType.MiningFee
            });
        var feeAmountRelayFeeRes = await connection.QuerySingleAsync<FeeAmount>(insertFeeAmount,
            new
            {
              fee = feeRes.Id,
              satoshis = fee.RelayFee.Satoshis,
              bytes = fee.RelayFee.Bytes,
              feeAmountType = Const.AmountType.RelayFee
            });

        feeRes.MiningFee = feeAmountMiningFeeRes;
        feeRes.RelayFee = feeAmountRelayFeeRes;
        feeResArr.Add(feeRes);
         
      }

      transaction.Commit();
      feeQuoteRes.Fees = feeResArr.ToArray();

      lock (cache)
      {
        cache.Clear();
      }
      return feeQuoteRes;
    }

    public static void EmptyRepository(string connectionString)
    {
      using var connection = new NpgsqlConnection(connectionString);
      RetryUtils.Exec(() => connection.Open());
      string cmdText = "TRUNCATE feeamount, fee, feequote CASCADE; ALTER SEQUENCE feequote_id_seq RESTART WITH 1;";
      connection.Execute(cmdText, null);

      lock (cache)
      {
        cache.Clear();
      }
    }

  }
}
