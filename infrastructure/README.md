# ObservaStock Infrastructure

This directory contains the Docker Compose setup for the ObservaStock observability stack.

## Components

- **OpenTelemetry Collector**: Receives telemetry data (traces, metrics, logs) from the services
- **Jaeger**: Distributed tracing backend and UI
- **Prometheus**: Metrics storage and querying
- **Grafana**: Visualization dashboard for metrics and traces

## Quick Start

### Prerequisites

- Docker and Docker Compose installed
- .NET 9 SDK installed (for running the services)

### 1. Start the Observability Stack

```bash
cd infrastructure
docker-compose up -d
```

This will start all the observability components:
- Jaeger UI: http://localhost:16686
- Prometheus: http://localhost:9090
- Grafana: http://localhost:3000 (admin/admin)
- OpenTelemetry Collector: gRPC endpoint at localhost:4317

### 2. Run the Services

In separate terminals, run the ObservaStock services:

**Terminal 1 - PriceService:**
```bash
cd ../src/ObservaStock.PriceService
dotnet run
```

**Terminal 2 - TradingApi:**
```bash
cd ../src/ObservaStock.TradingApi
dotnet run
```

### 3. Generate Some Traffic

Place a trade using curl or your favorite HTTP client:

```bash
curl -X POST http://localhost:5000/api/trades \
  -H "Content-Type: application/json" \
  -d '{
    "symbol": "AAPL",
    "action": "Buy",
    "quantity": 10
  }'
```

Or use multiple symbols to see variety:

```bash
# Buy Apple stock
curl -X POST http://localhost:5000/api/trades \
  -H "Content-Type: application/json" \
  -d '{"symbol": "AAPL", "action": "Buy", "quantity": 10}'

# Sell Microsoft stock
curl -X POST http://localhost:5000/api/trades \
  -H "Content-Type: application/json" \
  -d '{"symbol": "MSFT", "action": "Sell", "quantity": 5}'

# Buy Tesla stock
curl -X POST http://localhost:5000/api/trades \
  -H "Content-Type: application/json" \
  -d '{"symbol": "TSLA", "action": "Buy", "quantity": 15}'
```

### 4. View Observability Data

#### Distributed Traces (Jaeger)
1. Open http://localhost:16686
2. Select "ObservaStock.TradingApi" service
3. Click "Find Traces"
4. You'll see the full trace of a request from TradingApi → PriceService

#### Metrics (Grafana)
1. Open http://localhost:3000 (login: admin/admin)
2. Navigate to Explore
3. Select "Prometheus" datasource
4. Query examples:
   - `total_trades_placed_total` - Counter of all trades
   - `trade_value_usd_bucket` - Histogram of trade values
   - `rate(total_trades_placed_total[1m])` - Trades per minute
   - `histogram_quantile(0.95, rate(trade_value_usd_bucket[5m]))` - 95th percentile trade value

#### Metrics (Prometheus)
1. Open http://localhost:9090
2. Try queries like:
   - `total_trades_placed_total`
   - `trade_value_usd_count`
   - `trade_value_usd_sum`

## Architecture

```
┌─────────────────┐         ┌─────────────────┐
│   TradingApi    │────────▶│  PriceService   │
│   (port 5000)   │  HTTP   │   (port 5001)   │
└────────┬────────┘         └────────┬─────────┘
         │                           │
         │ OTLP                     │ OTLP
         │ (gRPC)                   │ (gRPC)
         ▼                           ▼
    ┌────────────────────────────────────┐
    │   OpenTelemetry Collector          │
    │         (port 4317)                │
    └────────┬──────────────┬────────────┘
             │              │
             │              │
        Traces│         Metrics│
             │              │
             ▼              ▼
    ┌─────────────┐  ┌─────────────┐
    │   Jaeger    │  │ Prometheus  │
    │ (port 16686)│  │ (port 9090) │
    └─────────────┘  └──────┬──────┘
                            │
                            │ Datasource
                            ▼
                     ┌─────────────┐
                     │   Grafana   │
                     │ (port 3000) │
                     └─────────────┘
```

## Custom Metrics

The TradingApi exposes custom metrics:

1. **total_trades_placed** (Counter)
   - Counts the total number of trades placed
   - Labels: `symbol`, `action` (Buy/Sell)

2. **trade_value_usd** (Histogram)
   - Records the USD value of each trade
   - Labels: `symbol`, `action` (Buy/Sell)
   - Useful for percentile calculations (p50, p95, p99)

## Random Latency

The PriceService introduces random latency (50-500ms) to simulate real-world service delays. This creates interesting patterns in the trace timeline and latency histograms.

## Stopping the Stack

```bash
cd infrastructure
docker-compose down
```

To also remove volumes:
```bash
docker-compose down -v
```

## Troubleshooting

### Services can't connect to OpenTelemetry Collector

If running services outside Docker, ensure the OTLP endpoint is set to `http://localhost:4317` in appsettings.json.

### No data in Jaeger

1. Check if OpenTelemetry Collector is running: `docker-compose ps`
2. Check collector logs: `docker-compose logs otel-collector`
3. Verify services are sending data by checking their logs

### No metrics in Prometheus

1. Check Prometheus targets: http://localhost:9090/targets
2. Verify OpenTelemetry Collector is exporting metrics: http://localhost:8889/metrics
3. Check collector configuration in `otel-collector-config.yaml`

## Next Steps

- Create custom Grafana dashboards for your metrics
- Set up alerts in Prometheus/Grafana
- Add more services to the distributed system
- Implement sampling strategies for high-traffic scenarios
