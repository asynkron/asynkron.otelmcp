# `Asynkron.JsEngine` Context

This project implements a lightweight JavaScript-inspired execution engine. Source code is parsed into S-expressions using a handwritten lexer and recursive descent parser. Expressions are represented as `Cons` cells populated with `Symbol` atoms so that the evaluator can treat JavaScript constructs as if they were Lisp forms.

Key components:

- `Lexer` / `Parser` – Convert JavaScript source into S-expressions.
- `Cons` / `Symbol` – Minimal cons cell and symbol types that underpin the S-expression tree.
- `Evaluator` – Walks the S-expression program, maintaining lexical environments, closures, host interop via `IJsCallable`, and materialises object literals into `Dictionary<string, object?>` instances with property access support.
- `JsEngine` – Public façade that exposes parsing and evaluation helpers and allows custom globals to be registered.

Tests validating the behaviour live under `tests/Asynkron.JsEngine.Tests`.
