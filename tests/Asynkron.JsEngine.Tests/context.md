# `Asynkron.JsEngine.Tests` Context

xUnit tests covering the JavaScript execution engine. The suite exercises:

- Lexing and parsing into `Cons`-based S-expressions.
- Evaluation of arithmetic, assignments, and expression statements.
- Function declarations, closures, and host interop registered through `JsEngine.SetGlobalFunction`.
