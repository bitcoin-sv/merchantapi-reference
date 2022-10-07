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

    public MapiMetrics mapiMetrics { init; get; }
    public BlockParserMetrics blockParserMetrics { init; get; }
    public RpcMultiClientMetrics rpcMultiClientMetrics { init; get; }
    public NotificationsMetrics notificationsMetrics { init; get; }
    public MempoolCheckerMetrics mempoolCheckerMetrics { init; get; }

    public abstract class ClassWithMetricsBase
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

    public class MapiMetrics : ClassWithMetricsBase
    {
      public override string MetricsPrefix => METRICS_PREFIX_MAPI;

      public Gauge AnyBitcoindResponding { init; get; }
      public Counter RequestSum { init; get; }
      public Counter TxAuthenticatedUser { init; get; }
      public Counter TxAnonymousUser { init; get; }
      public Counter TxSentToNode { init; get; }
      public Counter TxAcceptedByNode { init; get; }
      public Counter TxRejectedByNode { init; get; }
      public Counter TxSubmitException { init; get; }
      public Counter TxResponseSuccess { init; get; }
      public Counter TxResponseFailure { init; get; }

      public MapiMetrics()
      {
        AnyBitcoindResponding = CreateGauge("any_bitcoind_responding", "Status 1 if any bitcoind is responding.");
        RequestSum = CreateCounter("request_counter", "Number of processed requests.");
        TxAuthenticatedUser = CreateCounter("tx_authenticated_user_counter", "Number of transactions submitted by authenticated users.");
        TxAnonymousUser = CreateCounter("tx_anonymous_user_counter", "Number of transactions submitted by anonymous users.");
        TxSentToNode = CreateCounter("tx_sent_to_node_counter", "Number of transactions sent to node.");
        TxAcceptedByNode = CreateCounter("tx_accepted_by_node_counter", "Number of transactions accepted by node.");
        TxRejectedByNode = CreateCounter("tx_rejected_by_node_counter", "Number of transactions rejected by node.");
        TxSubmitException = CreateCounter("tx_submit_exception_counter", "Number of transactions with submit exception.");
        TxResponseSuccess = CreateCounter("tx_response_success_counter", "Number of success responses.");
        TxResponseFailure = CreateCounter("tx_response_failure_counter", "Number of failure responses.");
      }
    }

    public class BlockParserMetrics : ClassWithMetricsBase
    {
      public override string MetricsPrefix => METRICS_PREFIX_BLOCKPARSER;

      public Histogram BlockParsingDuration { init; get; }
      public Counter BestBlockHeight { init; get; }
      public Counter BlockParsed { init; get; }
      public Gauge BlockParsingQueue { init; get; }

      public BlockParserMetrics()
      {
        BlockParsingDuration = CreateHistogram("blockparsing_duration_seconds", "Histogram of time spent parsing blocks.");
        BestBlockHeight = CreateCounter("bestblockheight", "Best block height.");
        BlockParsed = CreateCounter("blockparsed_counter", "Number of blocks parsed.");
        BlockParsingQueue = CreateGauge("blockparsingqueue", "Blocks in queue for parsing.");
      }
    }

    public class RpcMultiClientMetrics : ClassWithMetricsBase
    {
      public override string MetricsPrefix => METRICS_PREFIX_RPCMULTICLIENT;

      public Histogram GetTxOutsDuration { init; get; }
      public Histogram SendRawTxsDuration { init; get; }

      public RpcMultiClientMetrics()
      {
        GetTxOutsDuration = CreateHistogram("gettxouts_duration_seconds", "Histogram of time spent waiting for gettxouts response from node.");
        SendRawTxsDuration = CreateHistogram("sendrawtxs_duration_seconds", "Histogram of time spent waiting for sendrawtransactions response from node.");
      }
    }

    public class NotificationsMetrics : ClassWithMetricsBase
    {
      public override string MetricsPrefix => METRICS_PREFIX_NOTIFICATIONS;

      public Counter SuccessfulCallbacks { init; get; }
      public Counter FailedCallbacks { init; get; }
      public Histogram CallbackDuration { init; get; }
      public Gauge NotificationsInQueue { init; get; }
      public Gauge NotificationsWithError { init; get; }

      public NotificationsMetrics()
      {
        SuccessfulCallbacks = CreateCounter("successful_callbacks_counter", "Number of successful callbacks.");
        FailedCallbacks = CreateCounter("failed_callbacks_counter", "Number of failed callbacks.");
        CallbackDuration = CreateHistogram("callback_duration_seconds", "Total duration of callbacks.");
        NotificationsInQueue = CreateGauge("notification_in_queue", "Queued notifications.");
        NotificationsWithError = CreateGauge("notification_with_error", "Notifications with error that are not queued, but processed separately.");
      }
    }

    public class MempoolCheckerMetrics : ClassWithMetricsBase
    {
      public override string MetricsPrefix => METRICS_PREFIX_MEMPOOLCHECKER;

      public Counter SuccessfulResubmits { init; get; }
      public Counter UnsuccessfulResubmits { init; get; }
      public Counter ExceptionsOnResubmit { init; get; }

      public Histogram GetRawMempoolDuration { init; get; }
      public Gauge MinTxInMempool { init; get; }
      public Gauge MaxTxInMempool { init; get; }
      public Histogram GetMissingTransactionsDuration { init; get; }
      public Counter TxMissing { init; get; }
      public Counter TxResponseSuccess { init; get; }
      public Counter TxResponseFailure { init; get; }
      public Counter TxMissingInputsMax { init; get; }

      public MempoolCheckerMetrics()
      {
        SuccessfulResubmits = CreateCounter("successful_resubmit_counter", "Number of all successful resubmits.");
        UnsuccessfulResubmits = CreateCounter("unsuccessful_resubmit_counter", "Number of all unsuccessful or interrupted resubmits.");
        ExceptionsOnResubmit = CreateCounter("exceptions_resubmit_counter", "Number of resubmits that interrupted with exception.");

        GetRawMempoolDuration = CreateHistogram("getrawmempool_duration_seconds", "Histogram of time spent waiting for getrawmempool response from node.");
        MinTxInMempool = CreateGauge("min_tx_in_mempool", "Minumum number of transactions in mempool per node.");
        MaxTxInMempool = CreateGauge("max_tx_in_mempool", "Maximum number of transactions in mempool per node.");
        GetMissingTransactionsDuration = CreateHistogram("getmissingtransactions_duration_seconds", "Histogram of database execution time for the query which transactions must be resubmitted.");
        TxMissing = CreateCounter("tx_missing_counter", "Number of missing transactions, that are resent to node.");
        TxResponseSuccess = CreateCounter("tx_response_success_counter", "Number of transactions with success response.");
        TxResponseFailure = CreateCounter("tx_response_failure_counter", "Number of transactions with failure response.");
        TxMissingInputsMax = CreateCounter("tx_missing_inputs_max_counter", "Number of transactions that reached MempoolCheckerMissingInputsRetries.");
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
