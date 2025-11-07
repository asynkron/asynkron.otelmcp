# `Asynkron.JsEngine` Context

This project implements a lightweight JavaScript-inspired execution engine. Source code is parsed into S-expressions using a handwritten lexer and recursive descent parser. Expressions are represented as `Cons` cells populated with `Symbol` atoms so that the evaluator can treat JavaScript constructs as if they were Lisp forms.

Key components:

- `Lexer` / `Parser` – Convert JavaScript source into S-expressions.
- `Cons` / `Symbol` – Minimal cons cell and symbol types that underpin the S-expression tree.
- `Evaluator` – Walks the S-expression program, maintaining lexical environments, closures, host interop via `IJsCallable`, and materialises object literals into prototype-aware `JsObject` instances with property access support. Method calls bind the object instance to the `this` symbol so functions can reference their receivers, and the `new` form wires constructor prototypes onto created objects while class declarations translate into constructor/prototype setups.
- Control flow keywords such as `if`, `while`, `do/while`, and `for` are parsed into dedicated S-expressions so the evaluator can execute branching logic, handle loop-scoped variables, and respect `break`/`continue` statements.
- `JsObject` – Lightweight dictionary that tracks a `__proto__` chain so property lookups can traverse prototypes.
- `JsEngine` – Public façade that exposes parsing and evaluation helpers and allows custom globals to be registered.

Tests validating the behaviour live under `tests/Asynkron.JsEngine.Tests`.
