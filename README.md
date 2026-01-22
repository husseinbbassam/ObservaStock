# ObservaStock

A distributed stock trading system demonstrating **Full-Stack Observability** using **OpenTelemetry** with .NET 9 and C# 13.

## Overview

ObservaStock is a microservices-based application that showcases distributed tracing, metrics collection, and observability best practices. The system simulates a stock trading platform with the following services:

- **TradingApi**: Web API that accepts Buy/Sell orders
- **PriceService**: Minimal API that returns mock stock prices with random latency
- **Shared Library**: Reusable OpenTelemetry configuration
- **Infrastructure**: Docker Compose setup for the observability stack (Jaeger, Prometheus, Grafana)

## Features

### OpenTelemetry Integration
- ✅ **Distributed Tracing**: W3C Trace Context propagation across services
- ✅ **Custom Metrics**: Counter and Histogram for trading operations
- ✅ **Automatic Instrumentation**: ASP.NET Core, HttpClient, and Entity Framework Core
- ✅ **Runtime Metrics**: .NET runtime and process metrics
- ✅ **OTLP Export**: All telemetry exported via OpenTelemetry Protocol to single collector
- ✅ **Trace-Correlated Logging**: Logs exported to OTLP with TraceId correlation
- ✅ **Request Size Monitoring**: Middleware to track HTTP payload sizes
- ✅ **Health Checks with Metrics**: Service health monitoring with status exported as metrics

### Custom Instrumentation
- **TradingMetrics Meter**: 
  - `total_trades_placed` (Counter): Tracks all trades with symbol and action labels
  - `trade_value_usd` (Histogram): Records trade values for percentile analysis
- **Request Size Histogram**:
  - `http_request_payload_size_bytes` (Histogram): Monitors 'Heavy' requests
- **Health Status Gauge**:
  - `health_status` (Gauge): Service health status (1=Healthy, 0=Unhealthy, -1=Degraded)

### Observability Stack
- **Jaeger**: Visualize distributed traces
- **Prometheus**: Store and query metrics
- **Grafana**: Create dashboards and alerts (includes pre-built dashboard)
- **OpenTelemetry Collector**: Collect, process, and export telemetry
- **Health Checks UI**: Real-time service health monitoring

## Architecture

```
┌─────────────────┐         ┌─────────────────┐
│   TradingApi    │────────▶│  PriceService   │
│   (port 5000)   │  HTTP   │   (port 5001)   │
└────────┬────────┘         └────────┬─────────┘
         │                           │
         │ OTLP (gRPC)              │ OTLP (gRPC)
         ▼                           ▼
    ┌────────────────────────────────────┐
    │   OpenTelemetry Collector          │
    └────────┬──────────────┬────────────┘
             │              │
        Traces│         Metrics│
             ▼              ▼
    ┌─────────────┐  ┌─────────────┐
    │   Jaeger    │  │ Prometheus  │
    └─────────────┘  └──────┬──────┘
                            │
                            ▼
                     ┌─────────────┐
                     │   Grafana   │
                     └─────────────┘
```

## Quick Start

### Prerequisites
- .NET 9 SDK
- Docker and Docker Compose

### 1. Clone the Repository
```bash
git clone https://github.com/husseinbbassam/ObservaStock.git
cd ObservaStock
```

### 2. Build the Solution
```bash
dotnet build
```

### 3. Start the Observability Stack
```bash
cd infrastructure
docker-compose up -d
```

### 4. Run the Services

**Terminal 1 - PriceService:**
```bash
cd src/ObservaStock.PriceService
dotnet run
```

**Terminal 2 - TradingApi:**
```bash
cd src/ObservaStock.TradingApi
dotnet run
```

### 5. Place a Trade
```bash
curl -X POST http://localhost:5000/api/trades \
  -H "Content-Type: application/json" \
  -d '{
    "symbol": "AAPL",
    "action": "Buy",
    "quantity": 10
  }'
```

### 6. Check Service Health
```bash
# Check PriceService health
curl http://localhost:5001/health

# Check TradingApi health (includes dependency checks)
curl http://localhost:5000/health

# View Health UI in browser
open http://localhost:5000/health-ui
```

### 7. View Observability Data
- **Jaeger UI**: http://localhost:16686 (traces with correlated logs)
- **Prometheus**: http://localhost:9090 (metrics)
- **Grafana**: http://localhost:3000 (dashboards - admin/admin)
  - Import the dashboard from `grafana/dashboard.json`

### 8. Explore the Enhancements
See [OPENTELEMETRY_ENHANCEMENTS.md](OPENTELEMETRY_ENHANCEMENTS.md) for detailed documentation on:
- Request payload size monitoring
- Health checks with metrics export
- Trace-correlated logging
- Pre-built Grafana dashboard

## Project Structure

```
ObservaStock/
├── src/
│   ├── ObservaStock.Shared/           # Shared OpenTelemetry configuration
│   │   └── OpenTelemetryExtensions.cs
│   ├── ObservaStock.TradingApi/       # Trading Web API
│   │   ├── Program.cs
│   │   └── appsettings.json
│   └── ObservaStock.PriceService/     # Price Minimal API
│       ├── Program.cs
│       └── appsettings.json
├── infrastructure/                     # Docker Compose setup
│   ├── docker-compose.yml
│   ├── otel-collector-config.yaml
│   ├── prometheus.yml
│   ├── grafana/
│   │   └── provisioning/
│   └── README.md
└── ObservaStock.sln
```

## Custom Metrics

### Counter: total_trades_placed
Tracks the total number of trades placed.
```promql
# Total trades
total_trades_placed_total

# Trades per minute
rate(total_trades_placed_total[1m])

# Trades by symbol
sum by (symbol) (total_trades_placed_total)
```

### Histogram: trade_value_usd
Records the USD value of each trade.
```promql
# 95th percentile trade value
histogram_quantile(0.95, rate(trade_value_usd_bucket[5m]))

# Average trade value
rate(trade_value_usd_sum[5m]) / rate(trade_value_usd_count[5m])

# Total trade volume
trade_value_usd_sum
```

## Key Concepts Demonstrated

1. **Distributed Tracing**: See how a single request flows through TradingApi → PriceService
2. **Context Propagation**: W3C Trace Context headers automatically propagated
3. **Custom Metrics**: Business-specific metrics (trades, trade values)
4. **Automatic Instrumentation**: Zero-code instrumentation for ASP.NET Core and HttpClient
5. **Latency Simulation**: Random delays in PriceService create interesting trace patterns
6. **Centralized Observability**: All telemetry collected by OTEL Collector

## Technology Stack

- **.NET 9**: Latest .NET runtime
- **C# 13**: Modern C# features
- **OpenTelemetry**: CNCF observability standard
- **Jaeger**: Distributed tracing backend
- **Prometheus**: Time-series metrics database
- **Grafana**: Metrics visualization
- **Docker**: Containerization for observability stack

## Learn More

- [OpenTelemetry .NET Documentation](https://opentelemetry.io/docs/languages/net/)
- [Infrastructure README](infrastructure/README.md)
- [Jaeger Documentation](https://www.jaegertracing.io/docs/)
- [Prometheus Documentation](https://prometheus.io/docs/)
- [Grafana Documentation](https://grafana.com/docs/)

## License

MIT License - See LICENSE file for details

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.
