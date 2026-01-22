using ObservaStock.Shared;

var builder = WebApplication.CreateBuilder(args);

// Add OpenTelemetry
builder.Services.AddObservaStockOpenTelemetry(
    serviceName: "ObservaStock.PriceService",
    serviceVersion: "1.0.0",
    otlpEndpoint: builder.Configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://localhost:4317"
);

// Add HTTP context accessor for request tracing
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Stock price endpoint with random latency
app.MapGet("/api/prices/{symbol}", async (string symbol, ILogger<Program> logger) =>
{
    // Inject random latency (50ms - 500ms) for interesting spikes in Grafana
    var random = new Random();
    var delayMs = random.Next(50, 501);
    
    logger.LogInformation("Getting price for {Symbol} with {Delay}ms delay", symbol, delayMs);
    
    await Task.Delay(delayMs);
    
    // Generate mock stock price
    var price = random.Next(50, 500) + random.NextDouble();
    var response = new
    {
        Symbol = symbol.ToUpper(),
        Price = Math.Round(price, 2),
        Currency = "USD",
        Timestamp = DateTime.UtcNow
    };
    
    logger.LogInformation("Returning price {Price} for {Symbol}", response.Price, symbol);
    
    return Results.Ok(response);
})
.WithName("GetStockPrice");

app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Service = "PriceService" }))
    .WithName("Health");

app.Run();
