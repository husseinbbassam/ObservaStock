using System.Diagnostics.Metrics;

/// <summary>
/// Middleware that records the size of request payloads into an OpenTelemetry Histogram.
/// This helps monitor 'Heavy' requests.
/// </summary>
public class RequestSizeMiddleware
{
    private readonly RequestDelegate _next;
    private readonly Histogram<long> _requestSizeHistogram;
    private readonly ILogger<RequestSizeMiddleware> _logger;

    public RequestSizeMiddleware(
        RequestDelegate next, 
        Meter meter, 
        ILogger<RequestSizeMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        
        // Create histogram for request payload size
        _requestSizeHistogram = meter.CreateHistogram<long>(
            name: "http_request_payload_size_bytes",
            unit: "bytes",
            description: "Size of HTTP request payloads in bytes");
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only measure requests with content (POST, PUT, PATCH)
        if (context.Request.ContentLength.HasValue && context.Request.ContentLength.Value > 0)
        {
            var requestSize = context.Request.ContentLength.Value;
            
            // Record the request size with relevant tags
            _requestSizeHistogram.Record(
                requestSize,
                new KeyValuePair<string, object?>("http.method", context.Request.Method),
                new KeyValuePair<string, object?>("http.route", context.Request.Path.Value ?? "/"));

            _logger.LogDebug(
                "Request size recorded: {Size} bytes for {Method} {Path}",
                requestSize,
                context.Request.Method,
                context.Request.Path);
        }

        await _next(context);
    }
}

/// <summary>
/// Extension method to register the RequestSizeMiddleware.
/// </summary>
public static class RequestSizeMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestSizeRecording(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestSizeMiddleware>();
    }
}
