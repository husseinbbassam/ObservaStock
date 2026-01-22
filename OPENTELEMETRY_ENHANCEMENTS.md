# OpenTelemetry Enhancements - Implementation Guide

This document describes the OpenTelemetry enhancements made to the ObservaStock trading system.

## Overview

The following enhancements have been implemented:

1. ✅ **OTLP Exporter Configuration** - Single collector endpoint for all telemetry
2. ✅ **Request Payload Size Middleware** - Monitor 'Heavy' requests
3. ✅ **Grafana Dashboard** - Comprehensive monitoring dashboard
4. ✅ **OpenTelemetry Logging with OTLP** - Trace-correlated logging
5. ✅ **Health Checks with Metrics Export** - Service health monitoring

---

## 1. OTLP Exporter Configuration

### Status: ✅ Verified

The OpenTelemetry configuration already uses the OTLP exporter pointing to a single collector endpoint.

**Configuration Location**: `src/ObservaStock.Shared/OpenTelemetryExtensions.cs`

**Collector Endpoint**: `http://localhost:4317` (configurable via `appsettings.json`)

**Telemetry Types Exported**:
- **Traces** → OTLP gRPC → Jaeger
- **Metrics** → OTLP gRPC → Prometheus
- **Logs** → OTLP gRPC → Console/Logging backend

---

## 2. Request Payload Size Middleware

### Implementation

**File**: `src/ObservaStock.TradingApi/RequestSizeMiddleware.cs`

This middleware records the size of HTTP request payloads into an OpenTelemetry Histogram.

### Metric Details

**Metric Name**: `http_request_payload_size_bytes`
**Type**: Histogram
**Unit**: bytes
**Description**: Size of HTTP request payloads in bytes

**Tags**:
- `http.method` - HTTP method (POST, PUT, PATCH, etc.)
- `http.route` - Request path

### Usage

The middleware is automatically registered in TradingApi's Program.cs:

```csharp
app.UseRequestSizeRecording();
```

### Querying in Prometheus/Grafana

```promql
# P99 request size
histogram_quantile(0.99, sum(rate(observastock_http_request_payload_size_bytes_bucket[5m])) by (le))

# P95 request size
histogram_quantile(0.95, sum(rate(observastock_http_request_payload_size_bytes_bucket[5m])) by (le))

# Average request size
rate(observastock_http_request_payload_size_bytes_sum[5m]) / rate(observastock_http_request_payload_size_bytes_count[5m])
```

---

## 3. Grafana Dashboard

### Location

**File**: `grafana/dashboard.json` (root of repository)

### Dashboard Panels

#### Panel 1: Trade Volume (Counter)
- **Metric**: `observastock_total_trades_placed_total`
- **Visualization**: Time series
- **Queries**:
  - Total trades per second: `sum(rate(observastock_total_trades_placed_total[1m]))`
  - Trades by symbol: `sum by (symbol) (rate(observastock_total_trades_placed_total[1m]))`

#### Panel 2: P99 Latency of PriceService (Histogram)
- **Metric**: `observastock_http_server_request_duration_bucket`
- **Visualization**: Time series
- **Queries**:
  - P99 latency: `histogram_quantile(0.99, sum(rate(observastock_http_server_request_duration_bucket{service_name="ObservaStock.PriceService"}[5m])) by (le))`
  - P95 latency
  - P50 latency (median)

#### Panel 3: System CPU & Memory Usage
- **Metrics**: 
  - CPU: `observastock_process_cpu_time_seconds_total`
  - Memory: `observastock_process_memory_usage_bytes`
- **Visualization**: Time series with dual Y-axes
- **Queries**:
  - CPU %: `rate(observastock_process_cpu_time_seconds_total[1m]) * 100`
  - Memory MB: `observastock_process_memory_usage_bytes / 1024 / 1024`

#### Panel 4: Request Payload Size (Histogram) [BONUS]
- **Metric**: `observastock_http_request_payload_size_bytes_bucket`
- **Visualization**: Time series
- **Shows**: P99, P95, P50 request sizes

#### Panel 5: Health Check Status [BONUS]
- **Metric**: `observastock_health_status`
- **Visualization**: Time series with value mappings
- **Values**: 1=Healthy (green), 0=Unhealthy (red), -1=Degraded (yellow)

### Importing the Dashboard

1. Navigate to Grafana UI: http://localhost:3000
2. Go to **Dashboards** → **Import**
3. Upload `grafana/dashboard.json`
4. Select Prometheus datasource
5. Click **Import**

---

## 4. OpenTelemetry Logging with OTLP

### Implementation

**File**: `src/ObservaStock.Shared/OpenTelemetryExtensions.cs`

The ILoggingBuilder is now configured to export logs to the OTLP exporter.

### Key Changes

```csharp
services.AddLogging(loggingBuilder =>
{
    loggingBuilder.AddOpenTelemetry(options =>
    {
        options.SetResourceBuilder(resourceBuilder);
        options.IncludeFormattedMessage = true;
        options.IncludeScopes = true;
        options.ParseStateValues = true;
        
        // NEW: Add OTLP exporter for logs
        options.AddOtlpExporter(otlpOptions =>
        {
            otlpOptions.Endpoint = new Uri(otlpEndpoint);
            otlpOptions.Protocol = OtlpExportProtocol.Grpc;
        });
    });
});
```

### Benefits

- **Trace Correlation**: Logs are automatically correlated with traces using TraceId
- **Centralized Collection**: All logs flow through the OTLP collector
- **Context Preservation**: Structured logging with full context (scopes, state values)

### Viewing Logs

Logs are currently exported to the OTLP collector and can be viewed in the collector's output. To persist and query logs, consider adding Loki to the infrastructure stack.

### Example Log Entry with TraceId

```json
{
  "timestamp": "2026-01-22T14:36:50.840Z",
  "level": "Information",
  "message": "Received trade request: Buy 10 shares of AAPL",
  "traceId": "843fff4ff2cebf52cce43e75c36267d4",
  "spanId": "a1b2c3d4e5f6g7h8",
  "service": "ObservaStock.TradingApi"
}
```

---

## 5. Health Checks with Metrics Export

### Implementation

**TradingApi**:
- `HealthCheckMetricsPublisher.cs` - Publishes health status as metrics
- Health checks for self and PriceService dependency
- Health Checks UI available at `/health-ui`

**PriceService**:
- Self health check

### Endpoints

#### TradingApi
- **Health Check**: `GET http://localhost:5000/health`
- **Health UI**: `GET http://localhost:5000/health-ui`

#### PriceService
- **Health Check**: `GET http://localhost:5001/health`

### Health Check Response Format

```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.0010836",
  "entries": {
    "PriceService": {
      "data": {},
      "duration": "00:00:00.0010144",
      "status": "Healthy",
      "tags": ["services", "priceservice"]
    },
    "self": {
      "data": {},
      "duration": "00:00:00.0000036",
      "status": "Healthy",
      "tags": ["services", "tradingapi"]
    }
  }
}
```

### Health Status Metric

**Metric Name**: `observastock_health_status`
**Type**: Gauge
**Unit**: status
**Description**: Health check status (1=Healthy, 0=Unhealthy, -1=Degraded)

**Tags**:
- `check_name` - Name of the health check
- `status` - Status text (Healthy/Unhealthy/Degraded)

### Querying Health Status

```promql
# Overall health status
observastock_health_status

# Filter by check name
observastock_health_status{check_name="PriceService"}

# Alert on unhealthy status
observastock_health_status == 0
```

### NuGet Packages Added

**TradingApi**:
- `AspNetCore.HealthChecks.UI` (9.0.0)
- `AspNetCore.HealthChecks.UI.Client` (9.0.0)
- `AspNetCore.HealthChecks.UI.InMemory.Storage` (9.0.0)
- `AspNetCore.HealthChecks.Uris` (9.0.0)

**PriceService**:
- `AspNetCore.HealthChecks.UI.Client` (9.0.0)

---

## Testing the Implementation

### 1. Start the Infrastructure

```bash
cd infrastructure
docker-compose up -d
```

### 2. Start the Services

**Terminal 1 - PriceService**:
```bash
cd src/ObservaStock.PriceService
dotnet run
```

**Terminal 2 - TradingApi**:
```bash
cd src/ObservaStock.TradingApi
dotnet run
```

### 3. Test Health Checks

```bash
# Check PriceService health
curl http://localhost:5001/health | jq .

# Check TradingApi health (includes PriceService check)
curl http://localhost:5000/health | jq .

# View Health UI in browser
open http://localhost:5000/health-ui
```

### 4. Place a Trade (Tests Request Size Middleware)

```bash
curl -X POST http://localhost:5000/api/trades \
  -H "Content-Type: application/json" \
  -d '{"symbol": "AAPL", "action": "Buy", "quantity": 10}'
```

### 5. View Observability Data

- **Jaeger (Traces with correlated logs)**: http://localhost:16686
- **Prometheus (Metrics)**: http://localhost:9090
- **Grafana (Dashboard)**: http://localhost:3000 (admin/admin)

---

## Verification Checklist

- [x] OTLP exporter uses single collector endpoint (http://localhost:4317)
- [x] Request size middleware records payload sizes in histogram
- [x] Grafana dashboard created with 3+ required panels
- [x] Logs exported to OTLP with TraceId correlation
- [x] Health checks exposed on /health endpoints
- [x] Health status exported as OpenTelemetry metrics
- [x] All services build without errors
- [x] Services start and communicate successfully
- [x] Code review completed and feedback addressed
- [x] Security scan passed (0 vulnerabilities)

---

## Architecture Diagram

```
┌─────────────────┐         ┌─────────────────┐
│   TradingApi    │────────▶│  PriceService   │
│   (port 5000)   │  HTTP   │   (port 5001)   │
│                 │◄────────│                 │
│  /health        │  Health │  /health        │
│  /health-ui     │  Check  │                 │
└────────┬────────┘         └────────┬─────────┘
         │                           │
         │ OTLP (Traces, Metrics, Logs)
         ▼                           ▼
    ┌────────────────────────────────────┐
    │   OpenTelemetry Collector          │
    │   (port 4317)                      │
    └────────┬──────────────┬────────────┘
             │              │
        Traces│         Metrics│  Logs
             ▼              ▼      ▼
    ┌─────────────┐  ┌──────────────┐
    │   Jaeger    │  │  Prometheus  │
    │ (port 16686)│  │  (port 9090) │
    └─────────────┘  └──────┬───────┘
                            │
                            ▼
                     ┌─────────────┐
                     │   Grafana   │
                     │ (port 3000) │
                     │  Dashboard  │
                     └─────────────┘
```

---

## Key Metrics Summary

### Business Metrics
- `total_trades_placed_total` - Trade volume counter
- `trade_value_usd_*` - Trade value histogram

### Performance Metrics
- `http_server_request_duration_*` - Request latency histogram
- `http_request_payload_size_bytes_*` - Request size histogram (NEW)

### System Metrics
- `process_cpu_time_seconds_total` - CPU usage
- `process_memory_usage_bytes` - Memory usage
- `health_status` - Health check status (NEW)

### Runtime Metrics
- `process_runtime_dotnet_gc_*` - GC metrics
- `process_runtime_dotnet_thread_pool_*` - Thread pool metrics

---

## Troubleshooting

### Health Checks Failing

1. Ensure both services are running
2. Check network connectivity between services
3. Verify configuration in `appsettings.json`
4. Check logs: `tail -f /path/to/service.log`

### Metrics Not Appearing in Prometheus

1. Verify OTLP collector is running: `docker ps | grep otel-collector`
2. Check collector endpoint: http://localhost:8888/metrics
3. Ensure services are sending telemetry (check logs)
4. Verify Prometheus scrape config

### Grafana Dashboard Not Loading

1. Import the dashboard JSON manually
2. Check Prometheus datasource configuration
3. Verify metric names match (check in Prometheus first)
4. Allow time for data to accumulate (5-10 minutes)

---

## Next Steps

1. **Add Loki** for log aggregation and querying
2. **Set up Alerting** based on health status and latency thresholds
3. **Create More Dashboards** for specific business metrics
4. **Implement Distributed Tracing** across more services
5. **Add Load Testing** to generate interesting telemetry data

---

## References

- [OpenTelemetry .NET Documentation](https://opentelemetry.io/docs/languages/net/)
- [ASP.NET Core Health Checks](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks)
- [Grafana Dashboard Best Practices](https://grafana.com/docs/grafana/latest/dashboards/)
- [PromQL Cheat Sheet](https://promlabs.com/promql-cheat-sheet/)

---

**Last Updated**: 2026-01-22  
**Author**: GitHub Copilot  
**Status**: ✅ Complete
