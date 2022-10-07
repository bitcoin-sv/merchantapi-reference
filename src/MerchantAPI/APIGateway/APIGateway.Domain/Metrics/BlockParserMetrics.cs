// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using Prometheus;

namespace MerchantAPI.APIGateway.Domain.Metrics
{
  public class BlockParserMetrics : MetricsBase
  {
    public override string MetricsPrefix => Const.METRICS_PREFIX_BLOCKPARSER;

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
}
