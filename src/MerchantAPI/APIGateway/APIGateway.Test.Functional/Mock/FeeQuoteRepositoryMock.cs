// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System;
using System.IO;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using MerchantAPI.Common.Clock;
using MerchantAPI.Common.Authentication;
using MerchantAPI.APIGateway.Domain.Models;
using MerchantAPI.APIGateway.Domain.Repositories;

namespace MerchantAPI.APIGateway.Test.Functional.Mock
{
  public class FeeQuoteRepositoryMock : IFeeQuoteRepository
  {
    public static double quoteExpiryMinutes;
    private List<FeeQuote> _feeQuotes;

    private readonly IClock clock;

    public FeeQuoteRepositoryMock(IClock clock)
    {
      this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public string FeeFileName { get; set; } = "feeQuotes.json";


    public string GetFunctionalTestSrcRoot() 
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
    public FeeQuote GetCurrentFeeQuoteByIdentity(UserAndIssuer identity)
    {
      string file = Path.Combine(GetFunctionalTestSrcRoot(), "Mock", "MockQuotes", FeeFileName);
      string jsonData = File.ReadAllText(file);

      // check json
      List<FeeQuote> feeQuotes = JsonConvert.DeserializeObject<List<FeeQuote>>(jsonData);
      feeQuotes.Where(x => x.CreatedAt == DateTime.MinValue).ToList().ForEach(x => x.CreatedAt = x.ValidFrom = clock.UtcNow());
      _feeQuotes = new List<FeeQuote>();
      _feeQuotes.AddRange(feeQuotes.OrderBy(x => x.CreatedAt));
      return GetCurrentFeeQuoteByIdentityFromLoadedFeeQuotes(identity);
    }

    public FeeQuote GetFeeQuoteById(long feeQuoteId)
    {
      throw new NotImplementedException();
    }

    public IEnumerable<FeeQuote> GetFeeQuotesByIdentity(UserAndIssuer identity)
    {
      throw new NotImplementedException();
    }

    public IEnumerable<FeeQuote> GetValidFeeQuotesByIdentity(UserAndIssuer feeQuoteIdentity)
    {
      if (_feeQuotes == null)
      {
        GetCurrentFeeQuoteByIdentity(feeQuoteIdentity); // fill from filename
      }     
      var filtered = _feeQuotes.Where(x => x.Identity == feeQuoteIdentity?.Identity &&
                                  x.IdentityProvider == feeQuoteIdentity?.IdentityProvider &&
                                  x.ValidFrom <= MockedClock.UtcNow && 
                                  x.ValidFrom >= MockedClock.UtcNow.AddMinutes(-quoteExpiryMinutes)).ToArray();
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
