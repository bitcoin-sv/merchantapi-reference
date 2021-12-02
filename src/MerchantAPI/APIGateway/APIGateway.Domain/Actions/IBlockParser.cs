// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain.Models;
using MerchantAPI.APIGateway.Domain.Models.Events;
using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Domain.Actions
{
  public interface IBlockParser
  {
    /// <summary>
    /// Check if database is empty and insert first block
    /// </summary>
    Task InitializeDB();

    Task NewBlockDiscoveredAsync(NewBlockDiscoveredEvent e);

    Task<BlockParserStatus> GetBlockParserStatusAsync();
  }
}
