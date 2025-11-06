# `src` Context

Source projects compiled by the solution live here. The primary project is [`Asynkron.OtelReceiver`](Asynkron.OtelReceiver/context.md), which hosts the OTLP gRPC server, storage layer, TraceLens tooling, and generated protobuf definitions. The folder now also contains the [`Asynkron.JsEngine`](Asynkron.JsEngine/context.md) runtime for experimenting with JavaScript-like code executed as S-expressions.

Other notable items:
- [`Asynkron.OtelReceiver.sln`](../Asynkron.OtelReceiver.sln) references this folder exclusively.
- Subdirectories mirror bounded concerns; follow each linked `context.md` to dive deeper.
  - [`Asynkron.OtelReceiver/context.md`](Asynkron.OtelReceiver/context.md)
  - [`Asynkron.JsEngine/context.md`](Asynkron.JsEngine/context.md)
