// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System.Threading.Tasks;

namespace MerchantAPI.Common.Startup
{
  public interface IStartupChecker
  {
    public Task<bool> CheckAsync(bool testingEnvironment=false);
  }
}
