using System;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.DependencyInjection;

namespace AspireShop.AppHost;

/// <summary>
/// Helpers for embedding the OtelMCP receiver into the Aspire Shop distributed application.
/// </summary>
internal static class OtelMcpExtensions
{
    internal const string ResourceName = "otelmcp";
    internal const string OtlpEndpointName = "otel";

    /// <summary>
    /// Registers the OtelMCP receiver project and exposes its OTLP gRPC endpoint so other
    /// Aspire resources can emit telemetry without additional configuration.
    /// </summary>
    public static IResourceBuilder<ProjectResource> AddOtelMcp(this IDistributedApplicationBuilder builder)
    {
        // Ensure the lifecycle hook that injects OTLP environment variables is present once.
        builder.Services.TryAddLifecycleHook<OtelMcpEnvironmentHook>();

        var otelCollector = builder
                .AddProject<Projects.Asynkron_OtelReceiver>(ResourceName)
                .WithEndpoint(OtlpEndpointName, e =>
                {
                    e.Port = 4317;          // public proxy port
                    e.UriScheme = "http";
                    e.Transport = "http2";
                    // do not set ASPNETCORE_URLS; Aspire will inject a random target port
                })
                
                // OTLP/HTTP on 4318 (HTTP/1.1)
                .WithEndpoint("otlp-http", e =>
                {
                    e.Port = 4318;
                    e.UriScheme = "http";
                    e.Transport = "http";
                });


            // Use SQLite by default so the collector does not require additional infrastructure.
           // .WithEnvironment("ConnectionStrings__DefaultConnection", "Data Source=otelmcp.db");

        return otelCollector;
    }
}
