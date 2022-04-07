// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using System.Threading.Tasks;

namespace MerchantAPI.APIGateway.Domain.Actions
{
  public interface IMempoolChecker
  {
    Task<bool> CheckMempoolAndResubmitTxs(int isBlockParserIdleMs);

    bool ExecuteCheckMempoolAndResubmitTxs { get; }
  }
}
