# `Asynkron.JsEngine` Context

This project hosts a lightweight JavaScript execution engine that lowers Esprima's
JavaScript AST into Lisp-style S-expressions and evaluates them using a small
Lisp interpreter. The design enables experimentation with Lisp semantics while
still authoring programs in familiar JavaScript syntax.

Key entry points:
- [`Parsing/JsSExpressionBuilder.cs`](Parsing/JsSExpressionBuilder.cs) converts
  Esprima AST nodes into `Cons`/`Nil` structures that mirror Lisp lists.
- [`Runtime/JsLispEngine.cs`](Runtime/JsLispEngine.cs) provides `Parse` and
  `Execute` helpers, special-form evaluation, and the default runtime
  environment/built-ins.
- [`SExpressions/SExpression.cs`](SExpressions/SExpression.cs) defines the core
  data structures (`Cons`, `Symbol`, `Literal`, `Nil`) plus helper utilities for
  constructing and iterating over proper lists.

The accompanying test suite (`tests/Asynkron.JsEngine.Tests`) demonstrates
end-to-end parsing and evaluation scenarios, including function closures and
mutability rules for `let`/`const` declarations.
