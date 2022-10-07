// Copyright(c) 2022 Bitcoin Association.
// Distributed under the Open BSV software license, see the accompanying file LICENSE

using Prometheus;

namespace MerchantAPI.APIGateway.Domain.Metrics
{
  public class NotificationsMetrics : MetricsBase
  {
    public override string MetricsPrefix => Const.METRICS_PREFIX_NOTIFICATIONS;

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
}
