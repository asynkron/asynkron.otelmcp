# `Asynkron.JsEngine` Context

This project contains a small JavaScript execution engine implemented in C#. JavaScript source code is parsed with Esprima and transformed into Lisp-style S-expressions composed of cons cells. The interpreter then evaluates those forms with a minimal environment that exposes numeric and logical built-ins plus user-defined functions.

Key components:
- `JsExecutionEngine` – orchestrates parsing and evaluation.
- `Parsing/` – `SExpressionBuilder` converts Esprima's AST to the cons-based representation defined in `SExpressions/`.
- `Evaluation/` – interpreter runtime including the lexical environment, built-in functions, and lambda closures.
- `SExpressions/` – data structures for `Cons`, `Nil`, literals, and symbols.

Tests live in `tests/Asynkron.JsEngine.Tests`, covering parsing output and basic evaluation scenarios.
