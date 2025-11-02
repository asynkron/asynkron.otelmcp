# `samples` Directory Context

This folder vendors third-party workloads that generate realistic OpenTelemetry traffic for development and manual testing.
Each subdirectory mirrors the upstream repository layout so future updates can be pulled in with minimal friction.

Current contents:

- [`AspireShop`](AspireShop) – copy of the .NET Aspire Shop distributed application used to simulate traffic across multiple services. The AspireShop.AppHost automatically configures all services to export telemetry to the OtelMCP collector via the `AddOtelMcp()` extension method and `OtelMcpEnvironmentHook` lifecycle hook, which injects OTLP environment variables into all Aspire projects.

When adding another workload, document it here and note the original upstream location for traceability.
