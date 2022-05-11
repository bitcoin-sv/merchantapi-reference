// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using System.IO;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using MerchantAPI.Common.Clock;
using MerchantAPI.Common.Test.Clock;
using MerchantAPI.Common.Authentication;
using MerchantAPI.APIGateway.Domain.Models;
using MerchantAPI.APIGateway.Domain.Repositories;
using Microsoft.Extensions.Options;
using MerchantAPI.APIGateway.Domain;
using Microsoft.Extensions.Configuration;
using MerchantAPI.APIGateway.Infrastructure.Repositories;

namespace MerchantAPI.APIGateway.Test.Functional.Mock
{
  public class FeeQuoteRepositoryMock : IFeeQuoteRepository
  {

    public double QuoteExpiryMinutes;

    private List<FeeQuote> _feeQuotes;

    readonly string connectionString;
    readonly IClock clock;
    readonly FeeQuoteRepositoryPostgres feeQuoteRepositoryPostgres;

    public FeeQuoteRepositoryMock(IOptions<AppSettings> appSettings, IConfiguration configuration, IClock clock)
    {
      this.connectionString = configuration["ConnectionStrings:DBConnectionStringDDL"];
      this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
      this.feeQuoteRepositoryPostgres = new FeeQuoteRepositoryPostgres(appSettings, configuration, clock);
    }

    public string FeeFileName { get; set; } = "feeQuotes.json";
    private string _feeFileName;


    private static string GetFunctionalTestSrcRoot()
    {
      string path = Directory.GetCurrentDirectory();

      string projectName = Path.Combine("APIGateway.Test.Functional");

      for (int i = 0; i < 6; i++)
      {
        string testData = Path.Combine(path, projectName);
        if (Directory.Exists(testData))
        {
          return testData;
        }
        path = Path.Combine(path, "..");
      }
      throw new Exception($"Can not find '{projectName}' near location {Directory.GetCurrentDirectory()}. Last processed path:{path}");
    }

    private FeeQuote GetCurrentFeeQuoteByIdentityFromLoadedFeeQuotes(UserAndIssuer identity)
    {
      return _feeQuotes.LastOrDefault(x =>
                 identity?.Identity == x?.Identity && identity?.IdentityProvider == x?.IdentityProvider);
    }

    private void EnsureFeeQuotesAreAvailable()
    {
      if (_feeQuotes == null || _feeFileName != FeeFileName)
      {
        _feeFileName = FeeFileName;
        // fill from filename
        string file = Path.Combine(GetFunctionalTestSrcRoot(), "Mock", "MockQuotes", FeeFileName);
        string jsonData = File.ReadAllText(file);

        // check json
        List<FeeQuote> feeQuotes = JsonConvert.DeserializeObject<List<FeeQuote>>(jsonData);
        feeQuotes.Where(x => x.CreatedAt == DateTime.MinValue).ToList().ForEach(x => x.CreatedAt = x.ValidFrom = clock.UtcNow());
        _feeQuotes = new List<FeeQuote>();
        _feeQuotes.AddRange(feeQuotes.OrderBy(x => x.CreatedAt));

        // we must also maintain data on database, because tx table references feeQuote table (for mAPI resilience)
        FeeQuoteRepositoryPostgres.EmptyRepository(connectionString);
        foreach (var feeQuote in _feeQuotes)
        {
          var insertedFee = feeQuoteRepositoryPostgres.InsertFeeQuoteAsync(feeQuote).Result;
          if (insertedFee == null)
          {
            throw new Exception("Problem with insert feeQuote");
          }
          feeQuote.Id = insertedFee.Id;
        }
      }
    }

    public FeeQuote GetCurrentFeeQuoteByIdentity(UserAndIssuer identity)
    {
      EnsureFeeQuotesAreAvailable();
      return GetCurrentFeeQuoteByIdentityFromLoadedFeeQuotes(identity);
    }

    public FeeQuote[] GetAllFeeQuotes()
    {
      EnsureFeeQuotesAreAvailable();
      return _feeQuotes.ToArray();
    }

    public FeeQuote GetFeeQuoteById(long feeQuoteId)
    {
      EnsureFeeQuotesAreAvailable();
      return _feeQuotes.Single(x => x.Id == feeQuoteId);
    }

    public IEnumerable<FeeQuote> GetFeeQuotesByIdentity(UserAndIssuer identity)
    {
      throw new NotImplementedException();
    }

    public IEnumerable<FeeQuote> GetValidFeeQuotesByIdentity(UserAndIssuer feeQuoteIdentity)
    {
      EnsureFeeQuotesAreAvailable();
      var filtered = _feeQuotes.Where(x => x.Identity == feeQuoteIdentity?.Identity &&
                                  x.IdentityProvider == feeQuoteIdentity?.IdentityProvider &&
                                  x.ValidFrom <= MockedClock.UtcNow && 
                                  x.ValidFrom >= MockedClock.UtcNow.AddMinutes(-QuoteExpiryMinutes)).ToArray();
      if (!filtered.Any())
      {
        var quote = GetCurrentFeeQuoteByIdentityFromLoadedFeeQuotes(feeQuoteIdentity);
        return new List<FeeQuote>() { quote };
      }
      return filtered;
    }

    public Task<FeeQuote> InsertFeeQuoteAsync(FeeQuote feeQuote)
    {
      throw new NotImplementedException();
    }

    public IEnumerable<FeeQuote> GetValidFeeQuotes()
    {
      throw new NotImplementedException();
    }

    public IEnumerable<FeeQuote> GetFeeQuotes()
    {
      throw new NotImplementedException();
    }

    public IEnumerable<FeeQuote> GetCurrentFeeQuotes()
    {
      throw new NotImplementedException();
    }
  }
}
