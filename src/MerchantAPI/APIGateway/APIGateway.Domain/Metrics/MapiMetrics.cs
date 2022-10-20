// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using Prometheus;

namespace MerchantAPI.APIGateway.Domain.Metrics
{
  public class MapiMetrics : MetricsBase
  {
    public override string MetricsPrefix => Const.METRICS_PREFIX_MAPI;

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
    public Counter TxMissingInputs { init; get; }
    public Counter TxReSentMissingInputs { init; get; }
    public Counter TxWasMinedMissingInputs { init; get; }
    public Counter TxInvalidBlockMissingInputs { init; get; }

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
      TxMissingInputs = CreateCounter("tx_missing_inputs_counter", "Number of transactions with missing inputs.");
      TxReSentMissingInputs = CreateCounter("tx_resent_missing_inputs_counter", "Number of transactions which reported missing inputs had its inputs resent.");
      TxWasMinedMissingInputs = CreateCounter("tx_was_mined_missing_inputs_counter", "Number of transactions which reported missing inputs and were discovered as mined.");
      TxInvalidBlockMissingInputs = CreateCounter("tx_invalid_block_missing_inputs_counter", "Number of transactions which reported missing inputs and were discovered in a block that was not on active chain.");
    }
  }
}
