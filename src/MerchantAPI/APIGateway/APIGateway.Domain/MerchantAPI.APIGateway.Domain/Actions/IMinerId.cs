// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Domain.Actions
{

  public interface IMinerId // Implementation needs to be thread safe, since this is registered as Singleton in startup
  {
    Task<string> GetCurrentMinerIdAsync();
    /// <summary>
    /// To avoid race conditions between GetCurrentMinerId() that is encoded in data that is about to be signed and SignWithMinerId,
    /// the SignWithMinerId() takes currentMinerId as input parameter, so that we request signature with the correct key.
    /// </summary>
    /// <returns></returns>
    Task<string> SignWithMinerIdAsync(string currentMinerId, string hash);
  }
}
