using System.Diagnostics.Metrics;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Publishes health check results as OpenTelemetry metrics.
/// This allows monitoring health status changes through the observability stack.
/// </summary>
public class HealthCheckMetricsPublisher : IHealthCheckPublisher
{
    private readonly Gauge<int> _healthStatusGauge;
    private readonly ILogger<HealthCheckMetricsPublisher> _logger;

    public HealthCheckMetricsPublisher(Meter meter, ILogger<HealthCheckMetricsPublisher> logger)
    {
        _logger = logger;
        
        // Create gauge for health status (1 = Healthy, 0 = Unhealthy, -1 = Degraded)
        _healthStatusGauge = meter.CreateGauge<int>(
            name: "health_status",
            unit: "status",
            description: "Health check status: 1=Healthy, 0=Unhealthy, -1=Degraded");
    }

    public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
    {
        var statusValue = report.Status switch
        {
            HealthStatus.Healthy => 1,
            HealthStatus.Degraded => -1,
            HealthStatus.Unhealthy => 0,
            _ => 0
        };

        // Record overall health status
        _healthStatusGauge.Record(
            statusValue,
            new KeyValuePair<string, object?>("status", report.Status.ToString()));

        // Record individual check statuses
        foreach (var entry in report.Entries)
        {
            var checkStatusValue = entry.Value.Status switch
            {
                HealthStatus.Healthy => 1,
                HealthStatus.Degraded => -1,
                HealthStatus.Unhealthy => 0,
                _ => 0
            };

            _healthStatusGauge.Record(
                checkStatusValue,
                new KeyValuePair<string, object?>("check_name", entry.Key),
                new KeyValuePair<string, object?>("status", entry.Value.Status.ToString()));

            _logger.LogDebug(
                "Health check {CheckName}: {Status}",
                entry.Key,
                entry.Value.Status);
        }

        return Task.CompletedTask;
    }
}
