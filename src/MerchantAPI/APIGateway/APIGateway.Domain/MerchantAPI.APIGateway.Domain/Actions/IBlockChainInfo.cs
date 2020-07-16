// Copyright (c) 2020 Bitcoin Association

using MerchantAPI.Common.BitcoinRpc.Responses;

namespace MerchantAPI.APIGateway.Domain.Actions
{

  public class ConsolidationTxParameters
  {
    public ConsolidationTxParameters()
    {
    }

    public ConsolidationTxParameters(RpcGetNetworkInfo networkInfo)
    {
      MinConsolidationFactor = networkInfo.MinConsolidationFactor;
      MinConsolidationInputMaturity = networkInfo.MinConsolidationInputMaturity;
      MaxConsolidationInputScriptSize = networkInfo.MaxConsolidationInputScriptSize;
      AcceptNonStdConsolidationInput = networkInfo.AcceptNonStdConsolidationInput;
    }

    public long MinConsolidationFactor { get; set; }

    public long MaxConsolidationInputScriptSize { get; set; }

    public long MinConsolidationInputMaturity { get; set; }

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
    BlockChainInfoData GetInfo();
  }
}
