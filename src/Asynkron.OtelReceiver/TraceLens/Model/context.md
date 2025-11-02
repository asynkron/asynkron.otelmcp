# `TraceLens/Model` Context

Core domain objects used for persisting and retrieving OTLP telemetry.

## Key types
- [`TraceLensModel.cs`](TraceLensModel.cs) – roots the span graph, links parent/child relationships, injects diagnostic log entries, and provides timeline helpers (`GetStartPercentD`, `GetWidthPercentD`, etc.).
- [`Span.cs`](Span.cs) – wraps OTLP spans with derived properties (duration, error detection, attribute lookups, recursive traversal).
- [`LogEntry.cs`](LogEntry.cs) – representation of logs and events tied to spans.
- [`Attributes.cs`](Attributes.cs) – `AttributeString` handles JSON/base64 decoding heuristics for attribute inspection.

These types are used by the Translator to convert OTLP data into the TraceLens model for storage and retrieval.
