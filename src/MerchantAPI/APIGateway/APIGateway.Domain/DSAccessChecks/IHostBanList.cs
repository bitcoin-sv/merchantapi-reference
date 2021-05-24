// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

namespace MerchantAPI.APIGateway.Domain.DSAccessChecks
{
  public interface IHostBanList
  {
    /// <summary>
    /// Increase ban score for given host
    /// </summary>
    void IncreaseBanScore(string host, int banScore);
    /// <summary>
    /// Check if the host is banned
    /// </summary>
    bool IsHostBanned(string host);
    /// <summary>
    /// Removes the host from ban-list (ONLY FOR TESTING !!!)
    /// </summary>
    void RemoveFromBanList(string host);
    /// <summary>
    /// Returns ban-score for the host (used in testing)
    /// </summary>
    /// <param name="host"></param>
    int ReturnBanScore(string host);
  }
}
