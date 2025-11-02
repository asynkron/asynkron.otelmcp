# `TraceLens/Infra` Context

Infrastructure helpers used across TraceLens components.

- [`ChannelExtensions.cs`](ChannelExtensions.cs) – adds `ReadBatchAsync` for backpressure-friendly channel consumption used by gRPC services.
- [`Extensions.cs`](Extensions.cs) – OTLP-focused helpers (service-name extraction, attribute value conversions, ByteString to hex conversions).
- [`Translator.cs`](Translator.cs) – `OtelTranslator` builds TraceLens models from OTLP spans/logs. It deduplicates spans, links logs/events, and produces `TraceLensModel` instances with optional diagnostics/multi-root support.

These helpers are tightly coupled to the domain classes described in [`../Model/context.md`](../Model/context.md). Update both contexts when extending translation logic or adding infrastructure utilities.
