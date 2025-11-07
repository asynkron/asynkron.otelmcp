# `Asynkron.JsEngine.Tests` Context

xUnit tests covering the JavaScript execution engine. The suite exercises:

- Lexing and parsing into `Cons`-based S-expressions.
- Evaluation of arithmetic, assignments, expression statements, object literal/property behaviour, and `this` binding during method calls, including nested closures that capture receivers for later invocation.
- Function declarations, closures, and host interop registered through `JsEngine.SetGlobalFunction`.
