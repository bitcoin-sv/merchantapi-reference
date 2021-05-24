// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System.Collections.Generic;
using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Domain.Models
{
  public interface INodes
  {
    Task<Node> CreateNodeAsync(Node node);
    int DeleteNode(string id);
    Node GetNode(string id);
    IEnumerable<Node> GetNodes();
    Task<bool> UpdateNodeAsync(Node node);
  }
}
