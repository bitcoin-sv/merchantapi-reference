// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using Prometheus;

namespace MerchantAPI.APIGateway.Domain.Metrics
{
  public abstract class MetricsBase
  {
    abstract public string MetricsPrefix { get; }
    public Counter CreateCounter(string name, string description)
    {
      return Prometheus.Metrics
      .CreateCounter($"{MetricsPrefix}{name}", description);
    }

    public Histogram CreateHistogram(string name, string description)
    {
      return Prometheus.Metrics
      .CreateHistogram($"{MetricsPrefix}{name}", description);
    }

    public Gauge CreateGauge(string name, string description)
    {
      return Prometheus.Metrics
      .CreateGauge($"{MetricsPrefix}{name}", description);
    }
  }
}
