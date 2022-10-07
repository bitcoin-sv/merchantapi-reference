using Prometheus;
// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

namespace MerchantAPI.APIGateway.Domain.Metrics
{
  public class RpcMultiClientMetrics : MetricsBase
  {
    public override string MetricsPrefix => Const.METRICS_PREFIX_RPCMULTICLIENT;

    public Histogram GetTxOutsDuration { init; get; }
    public Histogram SendRawTxsDuration { init; get; }

    public RpcMultiClientMetrics()
    {
      GetTxOutsDuration = CreateHistogram("gettxouts_duration_seconds", "Histogram of time spent waiting for gettxouts response from node.");
      SendRawTxsDuration = CreateHistogram("sendrawtxs_duration_seconds", "Histogram of time spent waiting for sendrawtransactions response from node.");
    }
  }
}
