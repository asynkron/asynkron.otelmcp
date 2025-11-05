# `Asynkron.OtelReceiver.Tests` Context

xUnit project covering repository persistence logic, SQLite bulk insert behaviour, and in-process gRPC ingestion tests. Comprehensive test coverage for all API branches: HTTPS gRPC OTLP collector, gRPC TraceLens DataService, HTTP REST bindings (JSON transcoding), and MCP API.

## Test Files

- [`SqliteSpanBulkInserterTests.cs`](SqliteSpanBulkInserterTests.cs) exercises:
  - `SqliteSpanBulkInserter.InsertAsync` to ensure batches are persisted correctly when `SaveChangesAsync` is invoked.
  - `ModelRepo.SaveTrace` attribute/span-name persistence using an in-memory SQLite database and `ReceiverMetricsCollector`.
  - `ModelRepo.SaveLogs`/`SaveMetrics` to verify log and metric entities are stored.

- [`OtelGrpcIngestionTests.cs`](OtelGrpcIngestionTests.cs) spins up the full ASP.NET Core host with `WebApplicationFactory`, pushes fake OTLP spans/logs/metrics through the gRPC endpoints, and asserts the database captures the payloads (including guards against duplicate metric persistence across multi-resource exports). Tests the **HTTPS gRPC OTLP collector** API branch.

- [`ReceiverMetricsServiceTests.cs`](ReceiverMetricsServiceTests.cs) verifies the `ReceiverMetricsService` gRPC streaming endpoint that exposes live metrics about telemetry ingestion. Tests server streaming functionality and validates that metrics updates are received when traces are ingested.

- [`DataServiceTests.cs`](DataServiceTests.cs) verifies the **gRPC TraceLens DataService** API branch (search, tag lookups, service map metadata) and asserts the composite AND/OR filter tree can isolate traces by multi-attribute expressions. It also checks that log-attribute predicates (including resource attributes) restrict hydrated logs when searching, covers the error/duration filters surfaced by `tracelens.proto`, and adds a regression ensuring span-attribute filters are enforced directly in SQL via the normalised `SpanAttributeValues` table (with evaluator fallbacks when `Span.AttributeMap` is empty).

- [`TraceLensHttpRestTests.cs`](TraceLensHttpRestTests.cs) exercises the **HTTP REST bindings (JSON transcoding)** API branch for TraceLens operations. Tests all REST endpoints exposed via google.api.http annotations in `tracelens.proto`:
  - `GET /v1/tracelens/search/data` - search metadata
  - `GET /v1/tracelens/tags/{TagName}/values` - tag values
  - `POST /v1/tracelens/traces:search` - trace search
  - `GET /v1/tracelens/traces/{trace_id}` - specific trace retrieval
  - `GET /v1/tracelens/traces:random` - random trace selection
  - `GET /v1/tracelens/metrics:names` - metric names
  - `GET /v1/tracelens/metrics/{name}` - specific metric data

- [`McpStreamingHttpTests.cs`](McpStreamingHttpTests.cs) exercises the streaming HTTP MCP endpoint to ensure it mirrors the TraceLens search and metadata commands exposed over gRPC. Tests HTTP/1.1 and HTTP/2 compatibility.

- [`McpComprehensiveTests.cs`](McpComprehensiveTests.cs) provides comprehensive coverage of the **MCP (Model Context Protocol) API** branch. Tests:
  - MCP handshake and connection establishment
  - All MCP commands: `getSearchData`, `getValuesForTag`, `searchTraces`, `getMetricNames`, `getMetric`
  - Error handling for invalid commands
  - Sequential command execution
  - Server-Sent Events (SSE) streaming protocol compliance

- [`GrpcIntegrationCollection.cs`](GrpcIntegrationCollection.cs) defines a shared xUnit collection to serialize gRPC integration tests against the same `OtelReceiverApplicationFactory` instance.

## Test Infrastructure

`SqliteTestDatabase` creates a shared in-memory SQLite connection for data-layer unit tests, while `OtelReceiverApplicationFactory` provisions a temporary SQLite file and HTTP/2 gRPC channel for end-to-end ingestion scenarios.

## Coverage Summary

All four API branches are now comprehensively tested:
1. ✅ **HTTPS gRPC OTLP Collector** - TraceService, LogsService, MetricsService, ReceiverMetricsService
2. ✅ **gRPC TraceLens DataService** - All search, metadata, and metric operations
3. ✅ **HTTP REST Bindings** - JSON transcoding for all TraceLens endpoints
4. ✅ **MCP API** - Server-Sent Events streaming with all supported commands

Add new tests here when changing data access patterns, metrics recording logic, or API behaviour, and update this context with any new fixtures/utilities.
