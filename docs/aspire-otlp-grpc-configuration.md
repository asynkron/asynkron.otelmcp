# Aspire OpenTelemetry gRPC Configuration

This document describes how the AspireShop sample applications are configured to send OpenTelemetry telemetry data to the OtelMCP receiver using the OTLP gRPC protocol.

## Overview

The OtelMCP receiver implements the OpenTelemetry Protocol (OTLP) gRPC service, allowing it to receive traces, logs, and metrics from OpenTelemetry-instrumented applications. The AspireShop sample is pre-configured to automatically export all telemetry to the embedded OtelMCP receiver using gRPC.

## Configuration Components

### 1. OtelMCP Receiver (gRPC Server)

The OtelMCP receiver is configured in `samples/AspireShop/AspireShop.AppHost/OtelMcpExtensions.cs` with the following settings:

- **Port**: 4317 (standard OTLP gRPC port)
- **Protocol**: HTTP/2 (required for gRPC)
- **Transport**: gRPC (via Kestrel HTTP/2 endpoint)

Key configuration:
```csharp
.WithEndpoint(OtlpEndpointName, endpoint =>
{
    endpoint.Port = 4317;
    endpoint.UriScheme = "http";
})
.WithEnvironment("ASPNETCORE_URLS", "http://0.0.0.0:4317")
.WithEnvironment("ASPNETCORE_Kestrel__EndpointDefaults__Protocols", "Http2")
```

### 2. Environment Variable Injection

The `OtelMcpEnvironmentHook` (in `samples/AspireShop/AspireShop.AppHost/OtelMcpEnvironmentHook.cs`) automatically injects the following environment variables into all Aspire project resources:

- **`OTEL_EXPORTER_OTLP_ENDPOINT`**: Set to the OtelMCP receiver's endpoint URL (e.g., `http://localhost:4317`)
- **`OTEL_EXPORTER_OTLP_PROTOCOL`**: Set to `grpc` to explicitly use the gRPC protocol

These environment variables follow the [OpenTelemetry Environment Variable Specification](https://opentelemetry.io/docs/specs/otel/protocol/exporter/).

### 3. OpenTelemetry Exporter Configuration

The `AspireShop.ServiceDefaults` project (in `samples/AspireShop/AspireShop.ServiceDefaults/Extensions.cs`) configures the OpenTelemetry exporters:

```csharp
private static IHostApplicationBuilder AddOpenTelemetryExporters(this IHostApplicationBuilder builder)
{
    var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

    if (useOtlpExporter)
    {
        builder.Services.AddOpenTelemetry().UseOtlpExporter();
    }

    return builder;
}
```

The `UseOtlpExporter()` method automatically:
- Reads the `OTEL_EXPORTER_OTLP_ENDPOINT` environment variable
- Reads the `OTEL_EXPORTER_OTLP_PROTOCOL` environment variable
- Configures OTLP exporters for traces, metrics, and logs
- Uses gRPC as the transport protocol when `OTEL_EXPORTER_OTLP_PROTOCOL=grpc`

## Data Flow

1. **Application Startup**: Aspire AppHost starts and launches the OtelMCP receiver on port 4317
2. **Environment Injection**: `OtelMcpEnvironmentHook` runs during the `AfterEndpointsAllocatedAsync` lifecycle phase and injects OTLP environment variables into all project resources
3. **Service Initialization**: Each Aspire service starts and calls `AddServiceDefaults()`, which configures OpenTelemetry with the injected environment variables
4. **Telemetry Export**: Applications generate telemetry (traces, logs, metrics) and export it via gRPC to the OtelMCP receiver
5. **Data Persistence**: OtelMCP receiver processes the OTLP gRPC requests and persists the data to SQLite

## Verification

The configuration is validated by integration tests in `tests/Asynkron.OtelReceiver.Tests/OtelGrpcIngestionTests.cs`, which verify that:
- The OtelMCP receiver accepts OTLP gRPC connections
- Traces are correctly received and persisted
- Logs are correctly received and persisted
- Metrics are correctly received and persisted

## Protocol Details

### OTLP gRPC Service Definitions

The OtelMCP receiver implements the following gRPC services defined by the OpenTelemetry Protocol:

- **`opentelemetry.proto.collector.trace.v1.TraceService`**: Accepts trace spans
- **`opentelemetry.proto.collector.logs.v1.LogsService`**: Accepts log records  
- **`opentelemetry.proto.collector.metrics.v1.MetricsService`**: Accepts metrics data points

These services are implemented in:
- `src/Asynkron.OtelReceiver/Services/TraceServiceImpl.cs`
- `src/Asynkron.OtelReceiver/Services/LogsServiceImpl.cs`
- `src/Asynkron.OtelReceiver/Services/MetricsServiceImpl.cs`

### gRPC Transport

The receiver uses Kestrel configured for HTTP/2, which is required for gRPC:
- HTTP/2 provides multiplexing, allowing multiple concurrent RPC calls over a single connection
- The receiver uses HTTP/2 cleartext (h2c) on port 4317 for local development
- Production deployments can add TLS by configuring HTTPS endpoints

## Manual Configuration

If you need to configure telemetry export manually (e.g., for a standalone service), set these environment variables:

```bash
export OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317
export OTEL_EXPORTER_OTLP_PROTOCOL=grpc
```

Then ensure your application uses the OpenTelemetry SDK with the OTLP exporter:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddOtlpExporter())
    .WithMetrics(metrics => metrics.AddOtlpExporter());

builder.Logging.AddOpenTelemetry(logging => logging.AddOtlpExporter());
```

## Troubleshooting

### Telemetry Not Appearing

If telemetry is not being received by the OtelMCP receiver:

1. **Verify the receiver is running**: Check that the `otelmcp` resource is started in the Aspire dashboard
2. **Check environment variables**: Ensure `OTEL_EXPORTER_OTLP_ENDPOINT` and `OTEL_EXPORTER_OTLP_PROTOCOL` are set correctly in the target service
3. **Verify network connectivity**: Ensure the service can reach port 4317 on the receiver
4. **Check logs**: Look for export errors in the application logs

### gRPC Connection Errors

Common gRPC connection issues:

- **HTTP/2 not enabled**: Ensure Kestrel is configured with `HttpProtocols.Http2`
- **Port conflicts**: Verify port 4317 is not in use by another service
- **Firewall rules**: Ensure port 4317 is accessible from the application containers

## References

- [OpenTelemetry Protocol Specification](https://opentelemetry.io/docs/specs/otel/protocol/)
- [OpenTelemetry .NET OTLP Exporter](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry.Exporter.OpenTelemetryProtocol)
- [OpenTelemetry Environment Variables](https://opentelemetry.io/docs/specs/otel/protocol/exporter/)
- [Aspire Service Defaults](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/service-defaults)
