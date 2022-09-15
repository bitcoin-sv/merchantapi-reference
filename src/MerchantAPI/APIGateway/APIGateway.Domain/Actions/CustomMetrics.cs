// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using Prometheus;

namespace MerchantAPI.APIGateway.Domain.Actions
{
  public class CustomMetrics
  {
    const string METRICS_PREFIX_MAPI = "merchantapi_mapi_";
    const string METRICS_PREFIX_BLOCKPARSER = "merchantapi_blockparser_";
    const string METRICS_PREFIX_RPCMULTICLIENT = "merchantapi_rpcmulticlient_";

    public readonly MapiMetrics mapiMetrics;
    public readonly BlockParserMetrics blockParserMetrics;
    public readonly RpcMultiClientMetrics rpcMultiClientMetrics;

    public abstract class IClassWithMetrics
    {
      abstract public string MetricsPrefix { get; }
      public Counter CreateCounter(string name, string description)
      {
        return Metrics
        .CreateCounter($"{MetricsPrefix}{name}", description);
      }

      public Histogram CreateHistogram(string name, string description)
      {
        return Metrics
        .CreateHistogram($"{MetricsPrefix}{name}", description);
      }

      public Gauge CreateGauge(string name, string description)
      {
        return Metrics
        .CreateGauge($"{MetricsPrefix}{name}", description);
      }
    }

    public class MapiMetrics : IClassWithMetrics
    {
      public override string MetricsPrefix => METRICS_PREFIX_MAPI;

      public readonly Counter requestSum;
      public readonly Counter txAuthenticatedUser;
      public readonly Counter txAnonymousUser;
      public readonly Counter txSentToNode;
      public readonly Counter txAcceptedByNode;
      public readonly Counter txRejectedByNode;
      public readonly Counter txSubmitException;
      public readonly Counter txResponseSuccess;
      public readonly Counter txResponseFailure;

      public MapiMetrics()
      {
        requestSum = CreateCounter("request_counter", "Number of processed requests.");
        txAuthenticatedUser = CreateCounter("tx_authenticated_user_counter", "Number of transactions submitted by authenticated users.");
        txAnonymousUser = CreateCounter("tx_anonymous_user_counter", "Number of transactions submitted by anonymous users.");
        txSentToNode = CreateCounter("tx_sent_to_node_counter", "Number of transactions sent to node.");
        txAcceptedByNode = CreateCounter("tx_accepted_by_node_counter", "Number of transactions accepted by node.");
        txRejectedByNode = CreateCounter("tx_rejected_by_node_counter", "Number of transactions rejected by node.");
        txSubmitException = CreateCounter("tx_submit_exception_counter", "Number of transactions with submit exception.");
        txResponseSuccess = CreateCounter("tx_response_success_counter", "Number of success responses.");
        txResponseFailure = CreateCounter("tx_response_failure_counter", "Number of failure responses.");
      }
    }

    public class BlockParserMetrics : IClassWithMetrics
    {
      public override string MetricsPrefix => METRICS_PREFIX_BLOCKPARSER;

      public readonly Histogram blockParsingDuration;
      public readonly Counter bestBlockHeight;
      public readonly Counter blockParsed;
      public readonly Gauge blockParsingQueue;

      public BlockParserMetrics()
      {
        blockParsingDuration = CreateHistogram("blockparsing_duration_seconds", "Histogram of time spent parsing blocks.");
        bestBlockHeight = CreateCounter("bestblockheight", "Best block height.");
        blockParsed = CreateCounter("blockparsed_counter", "Number of blocks parsed.");
        blockParsingQueue = CreateGauge("blockparsingqueue", "Blocks in queue for parsing.");
      }
    }

    public class RpcMultiClientMetrics : IClassWithMetrics
    {
      public override string MetricsPrefix => METRICS_PREFIX_RPCMULTICLIENT;

      public readonly Histogram getTxOutsDuration;
      public readonly Histogram sendRawTxsDuration;

      public RpcMultiClientMetrics()
      {
        getTxOutsDuration = CreateHistogram("gettxouts_duration_seconds", "Histogram of time spent waiting for gettxouts response from node.");
        sendRawTxsDuration = CreateHistogram("sendrawtxs_duration_seconds", "Histogram of time spent waiting for sendrawtransactions response from node.");
      }
    }

    public CustomMetrics()
    {
      mapiMetrics = new();
      blockParserMetrics = new();
      rpcMultiClientMetrics = new();
    }
  }
}
