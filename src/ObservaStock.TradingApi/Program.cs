using System.Diagnostics.Metrics;
using ObservaStock.Shared;

var builder = WebApplication.CreateBuilder(args);

// Add OpenTelemetry
builder.Services.AddObservaStockOpenTelemetry(
    serviceName: "ObservaStock.TradingApi",
    serviceVersion: "1.0.0",
    otlpEndpoint: builder.Configuration["OpenTelemetry:OtlpEndpoint"] ?? "http://localhost:4317"
);

// Add custom meter for Trading Metrics
var tradingMeter = new Meter("TradingMetrics");
builder.Services.AddSingleton(tradingMeter);

// Create custom metrics
var totalTradesPlacedCounter = tradingMeter.CreateCounter<long>("total_trades_placed", "trades", "Total number of trades placed");
var tradeValueHistogram = tradingMeter.CreateHistogram<double>("trade_value_usd", "USD", "Value of trades in USD");

// Add HttpClient for calling PriceService
builder.Services.AddHttpClient("PriceService", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["PriceService:BaseUrl"] ?? "http://localhost:5001");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Buy/Sell order endpoint
app.MapPost("/api/trades", async (TradeRequest request, IHttpClientFactory httpClientFactory, ILogger<Program> logger) =>
{
    logger.LogInformation("Received trade request: {Action} {Quantity} shares of {Symbol}", 
        request.Action, request.Quantity, request.Symbol);

    try
    {
        // Call PriceService to get current stock price
        var httpClient = httpClientFactory.CreateClient("PriceService");
        var priceResponse = await httpClient.GetFromJsonAsync<PriceResponse>($"/api/prices/{request.Symbol}");
        
        if (priceResponse == null)
        {
            logger.LogError("Failed to get price for {Symbol}", request.Symbol);
            return Results.BadRequest(new { Error = "Failed to get stock price" });
        }

        logger.LogInformation("Retrieved price {Price} for {Symbol}", priceResponse.Price, request.Symbol);

        // Calculate trade value
        var tradeValue = priceResponse.Price * request.Quantity;
        
        // Record metrics
        totalTradesPlacedCounter.Add(1, new KeyValuePair<string, object?>("symbol", request.Symbol), 
                                           new KeyValuePair<string, object?>("action", request.Action));
        tradeValueHistogram.Record(tradeValue, new KeyValuePair<string, object?>("symbol", request.Symbol),
                                                new KeyValuePair<string, object?>("action", request.Action));

        var response = new TradeResponse
        {
            TradeId = Guid.NewGuid().ToString(),
            Symbol = request.Symbol,
            Action = request.Action,
            Quantity = request.Quantity,
            Price = priceResponse.Price,
            TotalValue = Math.Round(tradeValue, 2),
            Timestamp = DateTime.UtcNow,
            Status = "Completed"
        };

        logger.LogInformation("Trade completed: {TradeId}, Value: {TotalValue} USD", response.TradeId, response.TotalValue);

        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error processing trade for {Symbol}", request.Symbol);
        return Results.Problem("An error occurred processing the trade");
    }
})
.WithName("PlaceTrade");

app.MapGet("/api/trades/health", () => Results.Ok(new { Status = "Healthy", Service = "TradingApi" }))
    .WithName("Health");

app.Run();

// DTOs
record TradeRequest
{
    public required string Symbol { get; init; }
    public required string Action { get; init; } // "Buy" or "Sell"
    public required int Quantity { get; init; }
}

record TradeResponse
{
    public required string TradeId { get; init; }
    public required string Symbol { get; init; }
    public required string Action { get; init; }
    public required int Quantity { get; init; }
    public required double Price { get; init; }
    public required double TotalValue { get; init; }
    public required DateTime Timestamp { get; init; }
    public required string Status { get; init; }
}

record PriceResponse
{
    public required string Symbol { get; init; }
    public required double Price { get; init; }
    public required string Currency { get; init; }
    public required DateTime Timestamp { get; init; }
}
