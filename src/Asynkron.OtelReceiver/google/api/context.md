# Google API Proto Definitions

This directory contains Google API proto definitions required for gRPC-to-HTTP transcoding:

- [`annotations.proto`](annotations.proto) – Defines the `google.api.http` option extension that annotates gRPC methods
  with HTTP bindings (GET, POST, path templates, etc.).
- [`http.proto`](http.proto) – Defines the HttpRule message and related types used by annotations.proto for mapping
  gRPC methods to RESTful HTTP endpoints.

These proto files are included solely for import by other proto definitions (e.g., `tracelens.proto`). They are marked
with `CompileOutputs=false` in the project file to prevent C# code generation, as the Google.Api.CommonProtos NuGet
package already provides the compiled types. This approach enables HTTP transcoding without duplicating type definitions.

For more information on gRPC-HTTP transcoding, see:
- https://cloud.google.com/endpoints/docs/grpc/transcoding
- https://github.com/googleapis/googleapis
