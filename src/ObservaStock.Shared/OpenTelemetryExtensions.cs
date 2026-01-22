using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Instrumentation.EntityFrameworkCore;
using OpenTelemetry.Instrumentation.Http;
using OpenTelemetry.Instrumentation.Process;
using OpenTelemetry.Instrumentation.Runtime;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace ObservaStock.Shared;

/// <summary>
/// Extension methods for configuring OpenTelemetry in ObservaStock services.
/// </summary>
public static class OpenTelemetryExtensions
{
    /// <summary>
    /// Adds OpenTelemetry with Tracing, Metrics, and Logging configured for ObservaStock.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="serviceName">The name of the service.</param>
    /// <param name="serviceVersion">The version of the service.</param>
    /// <param name="otlpEndpoint">The OTLP endpoint URL (default: http://localhost:4317).</param>
    /// <param name="additionalMeterNames">Additional meter names to include in metrics collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddObservaStockOpenTelemetry(
        this IServiceCollection services,
        string serviceName,
        string serviceVersion = "1.0.0",
        string otlpEndpoint = "http://localhost:4317",
        params string[] additionalMeterNames)
    {
        // Create resource attributes for the service
        var resourceBuilder = ResourceBuilder
            .CreateDefault()
            .AddService(serviceName: serviceName, serviceVersion: serviceVersion)
            .AddAttributes(new Dictionary<string, object>
            {
                ["deployment.environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development",
                ["service.instance.id"] = Environment.MachineName
            });

        // Configure OpenTelemetry
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName, serviceVersion: serviceVersion))
            .WithTracing(tracerProviderBuilder =>
            {
                tracerProviderBuilder
                    .SetResourceBuilder(resourceBuilder)
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.EnrichWithHttpRequest = (activity, httpRequest) =>
                        {
                            activity.SetTag("http.request.method", httpRequest.Method);
                            activity.SetTag("http.request.path", httpRequest.Path);
                        };
                        options.EnrichWithHttpResponse = (activity, httpResponse) =>
                        {
                            activity.SetTag("http.response.status_code", httpResponse.StatusCode);
                        };
                    })
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.RecordException = true;
                        options.EnrichWithHttpRequestMessage = (activity, httpRequestMessage) =>
                        {
                            activity.SetTag("http.request.uri", httpRequestMessage.RequestUri?.ToString());
                        };
                        options.EnrichWithHttpResponseMessage = (activity, httpResponseMessage) =>
                        {
                            activity.SetTag("http.response.status_code", (int)httpResponseMessage.StatusCode);
                        };
                    })
                    .AddEntityFrameworkCoreInstrumentation(options =>
                    {
                        options.EnrichWithIDbCommand = (activity, command) =>
                        {
                            activity.SetTag("db.operation", command.CommandText);
                        };
                    })
                    .AddSource(serviceName)
                    .SetSampler(new AlwaysOnSampler())
                    .AddOtlpExporter(otlpOptions =>
                    {
                        otlpOptions.Endpoint = new Uri(otlpEndpoint);
                        otlpOptions.Protocol = OtlpExportProtocol.Grpc;
                    });
            })
            .WithMetrics(meterProviderBuilder =>
            {
                meterProviderBuilder
                    .SetResourceBuilder(resourceBuilder)
                    .AddRuntimeInstrumentation()
                    .AddProcessInstrumentation()
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddMeter(serviceName);
                
                // Add any additional meters
                foreach (var meterName in additionalMeterNames)
                {
                    meterProviderBuilder.AddMeter(meterName);
                }
                
                meterProviderBuilder.AddOtlpExporter(otlpOptions =>
                {
                    otlpOptions.Endpoint = new Uri(otlpEndpoint);
                    otlpOptions.Protocol = OtlpExportProtocol.Grpc;
                });
            });

        // Configure logging to include OpenTelemetry with OTLP export
        services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.AddOpenTelemetry(options =>
            {
                options.SetResourceBuilder(resourceBuilder);
                options.IncludeFormattedMessage = true;
                options.IncludeScopes = true;
                options.ParseStateValues = true;
                
                // Add OTLP exporter for logs to enable correlation with traces
                options.AddOtlpExporter(otlpOptions =>
                {
                    otlpOptions.Endpoint = new Uri(otlpEndpoint);
                    otlpOptions.Protocol = OtlpExportProtocol.Grpc;
                });
            });
        });

        return services;
    }

    /// <summary>
    /// Adds custom OpenTelemetry instrumentation for a specific service.
    /// This allows services to add their own custom meters and sources.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="meterName">The name of the custom meter.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCustomMeter(
        this IServiceCollection services,
        string meterName)
    {
        services.AddSingleton(sp => new Meter(meterName));
        return services;
    }
}
