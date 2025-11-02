# `TraceLens` Context

TraceLens is the analytical layer that interprets persisted spans/logs/metrics into higher-level component and timeline views.

## Structure
- [`Extensions.cs`](Extensions.cs) – shared helpers for timestamp conversions (Unix nanoseconds to DateTimeOffset and vice versa, time rounding for bucketing).
- [`Infra/context.md`](Infra/context.md) – runtime utilities (channel helpers, OTLP translators).
- [`Model/context.md`](Model/context.md) – core domain model: spans, logs, and attributes used for storing and retrieving telemetry.
- [`ExtendedProtos/context.md`](ExtendedProtos/context.md) – partial classes augmenting generated OTLP metric types with convenience APIs.

The TraceLens code is used by gRPC services for storing and querying telemetry data.
