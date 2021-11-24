// Copyright(c) 2020 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using MerchantAPI.Common.BitcoinRpc.Responses;
using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Domain.Actions
{

  public class ConsolidationTxParameters
  {
    public ConsolidationTxParameters()
    {
    }

    public ConsolidationTxParameters(RpcGetNetworkInfo networkInfo)
    {
      Version = networkInfo.Version;
      MinConsolidationFactor = networkInfo.MinConsolidationFactor;
      MinConfConsolidationInput = networkInfo.MinConsolidationInputMaturity;
      MaxConsolidationInputScriptSize = networkInfo.MaxConsolidationInputScriptSize;
      AcceptNonStdConsolidationInput = networkInfo.AcceptNonStdConsolidationInput;
    }

    public long Version { get; set; }

    public long MinConsolidationFactor { get; set; }

    public long MaxConsolidationInputScriptSize { get; set; }

    public long MinConfConsolidationInput { get; set; } // deprecated MinConsolidationInputMaturity

    public bool AcceptNonStdConsolidationInput { get; set; }

  }

  public class BlockChainInfoData
  {

    public BlockChainInfoData(string bestBlockHash, long bestBlockHeight, ConsolidationTxParameters consolidationTxParameters)
    {
      this.BestBlockHeight = bestBlockHeight;
      this.BestBlockHash = bestBlockHash;
      this.ConsolidationTxParameters = consolidationTxParameters;
    }
    public string BestBlockHash { get; }
    public long BestBlockHeight{ get; }

    // We keep consolidation parameters to BlockChainInfoData because we want to cache them too
    // (they can only change with node restarts)
    public ConsolidationTxParameters ConsolidationTxParameters { get; }
  }

  public interface IBlockChainInfo 
  { 
    Task<BlockChainInfoData> GetInfoAsync();
  }
}
