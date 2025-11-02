# `TraceLens` Context

TraceLens is the analytical layer that interprets persisted spans/logs/metrics into higher-level component and timeline views.

## Structure
- [`Extensions.cs`](Extensions.cs) & [`StringUtils.cs`](StringUtils.cs) – shared helpers for formatting timestamps, numeric values, and text.
- [`Infra/context.md`](Infra/context.md) – runtime utilities (channel helpers, OTLP translators, JSON flattening, PlantUML styling).
- [`Model/context.md`](Model/context.md) – core domain model: spans, logs, and attributes used for storing and retrieving telemetry.
- [`ExtendedProtos/context.md`](ExtendedProtos/context.md) – partial classes augmenting generated OTLP metric types with convenience APIs.

The TraceLens code is used by gRPC services for storing and querying telemetry data.
