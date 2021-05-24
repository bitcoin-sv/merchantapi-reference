// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.APIGateway.Domain.Models;
using System.Collections.Generic;

namespace MerchantAPI.APIGateway.Domain.Repositories
{
  public interface INodeRepository
  {

    /// <summary>
    /// Returns null if node already exists
    /// </summary>
    Node CreateNode(Node node);

    /// <summary>
    /// Returns false if not found, Can not be used to update nodeStatus
    /// </summary>
    bool UpdateNode(Node node);

    /// <summary>
    /// Updates lastError and lastErrorAt fields
    /// </summary>
    /// <returns>false if not updated</returns>
    bool UpdateNodeError(Node node);

    Node GetNode(string hostAndPort);

    int DeleteNode(string hostAndPort);

    public IEnumerable<Node> GetNodes();
  }
}
