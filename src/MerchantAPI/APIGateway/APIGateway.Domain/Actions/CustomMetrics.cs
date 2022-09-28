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
    const string METRICS_PREFIX_NOTIFICATIONS = "merchantapi_notificationshandler_";
    const string METRICS_PREFIX_MEMPOOLCHECKER = "merchantapi_mempoolchecker_";

    public readonly MapiMetrics mapiMetrics;
    public readonly BlockParserMetrics blockParserMetrics;
    public readonly RpcMultiClientMetrics rpcMultiClientMetrics;
    public readonly NotificationsMetrics notificationsMetrics;
    public readonly MempoolCheckerMetrics mempoolCheckerMetrics;

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

    public class NotificationsMetrics : IClassWithMetrics
    {
      public override string MetricsPrefix => METRICS_PREFIX_NOTIFICATIONS;

      public readonly Counter successfulCallbacks;
      public readonly Counter failedCallbacks;
      public readonly Histogram callbackDuration;

      public NotificationsMetrics()
      {
        successfulCallbacks = CreateCounter("successful_callbacks_counter", "Number of successful callbacks.");
        failedCallbacks = CreateCounter("failed_callbacks_counter", "Number of failed callbacks.");
        callbackDuration = CreateHistogram("callback_duration_seconds", "Total duration of callbacks.");
      }
    }

    public class MempoolCheckerMetrics : IClassWithMetrics
    {
      public override string MetricsPrefix => METRICS_PREFIX_MEMPOOLCHECKER;

      public readonly Counter successfulResubmits;
      public readonly Counter unsuccessfulResubmits;
      public readonly Counter exceptionsOnResubmit;

      public readonly Histogram getRawMempoolDuration;
      public readonly Gauge txInMempool;
      public readonly Histogram getMissingTransactionsDuration;
      public readonly Counter txMissing;
      public readonly Counter txResponseSuccess;
      public readonly Counter txResponseFailure;
      public readonly Counter txMissingInputsMax;

      public MempoolCheckerMetrics()
      {
        successfulResubmits = CreateCounter("successful_resubmit_counter", "Number of all successful resubmits.");
        unsuccessfulResubmits = CreateCounter("unsuccessful_resubmit_counter", "Number of all unsuccessful or interrupted resubmits.");
        exceptionsOnResubmit = CreateCounter("exceptions_resubmit_counter", "Number of resubmits that interrupted with exception.");

        getRawMempoolDuration = CreateHistogram("getrawmempool_duration_seconds", "Histogram of time spent waiting for getrawmempool response from node.");
        txInMempool = CreateGauge("tx_in_mempool", "Number of transactions in mempool.");
        getMissingTransactionsDuration = CreateHistogram("getmissingtransactions_duration_seconds", "Histogram of database execution time for the query which transactions must be resubmitted.");
        txMissing = CreateCounter("tx_missing_counter", "Number of missing transactions, that are resent to node.");
        txResponseSuccess = CreateCounter("tx_response_success_counter", "Number of transactions with success response.");
        txResponseFailure = CreateCounter("tx_response_failure_counter", "Number of transactions with failure response.");
        txMissingInputsMax = CreateCounter("tx_missing_inputs_max_counter", "Number of transactions that reached MempoolCheckerMissingInputsRetries.");
      }
    }

    public CustomMetrics()
    {
      mapiMetrics = new();
      blockParserMetrics = new();
      rpcMultiClientMetrics = new();
      notificationsMetrics = new();
      mempoolCheckerMetrics = new();
    }
  }
}
