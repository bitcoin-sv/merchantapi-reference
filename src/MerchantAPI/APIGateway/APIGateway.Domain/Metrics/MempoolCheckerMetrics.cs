// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using Prometheus;

namespace MerchantAPI.APIGateway.Domain.Metrics
{
  public class MempoolCheckerMetrics : MetricsBase
  {
    public override string MetricsPrefix => Const.METRICS_PREFIX_MEMPOOLCHECKER;

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
}
