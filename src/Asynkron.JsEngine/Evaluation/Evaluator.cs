using System;
using System.Collections.Generic;
using System.Linq;
using Asynkron.JsEngine.SExpressions;

namespace Asynkron.JsEngine.Evaluation;

/// <summary>
/// Evaluates S-expressions produced from JavaScript code.
/// </summary>
internal static class Evaluator
{
    public static object? Evaluate(SExpr expression, Environment environment)
        => expression switch
        {
            Nil => null,
            NullLiteral => null,
            NumberLiteral number => number.Value,
            StringLiteral str => str.Value,
            BooleanLiteral boolean => boolean.Value,
            Symbol symbol => environment.Lookup(symbol.Name),
            Cons cons => EvaluateList(cons, environment),
            _ => throw new NotSupportedException($"Unsupported S-expression type '{expression.GetType().Name}'.")
        };

    private static object? EvaluateList(Cons cons, Environment environment)
    {
        var items = cons.ToList();
        if (items.Count == 0)
        {
            return null;
        }

        if (items[0] is not Symbol head)
        {
            var callable = Evaluate(items[0], environment);
            var arguments = EvaluateArguments(items.Skip(1), environment);
            return Invoke(callable, arguments);
        }

        switch (head.Name)
        {
            case "begin":
                return EvaluateBegin(items.Skip(1), environment);
            case "let":
                return EvaluateLet(items, environment);
            case "set":
                return EvaluateSet(items, environment);
            case "if":
                return EvaluateIf(items, environment);
            case "function":
                return EvaluateFunction(items, environment);
            case "lambda":
                return EvaluateLambda(items, environment);
            case "call":
                return EvaluateCall(items, environment);
            case "member":
                return EvaluateMember(items, environment);
            case "return":
                return EvaluateReturn(items, environment);
            default:
                var callable = environment.Lookup(head.Name);
                var arguments = EvaluateArguments(items.Skip(1), environment);
                return Invoke(callable, arguments);
        }
    }

    private static object? EvaluateBegin(IEnumerable<SExpr> expressions, Environment environment)
    {
        object? result = null;
        foreach (var expression in expressions)
        {
            result = Evaluate(expression, environment);
        }

        return result;
    }

    private static object? EvaluateLet(IReadOnlyList<SExpr> items, Environment environment)
    {
        if (items.Count != 3 || items[1] is not Symbol symbol)
        {
            throw new InvalidOperationException("let expects a symbol name and a value.");
        }

        var value = Evaluate(items[2], environment);
        environment.Define(symbol.Name, value);
        return value;
    }

    private static object? EvaluateSet(IReadOnlyList<SExpr> items, Environment environment)
    {
        if (items.Count != 3 || items[1] is not Symbol symbol)
        {
            throw new InvalidOperationException("set expects a symbol name and a value.");
        }

        var value = Evaluate(items[2], environment);
        environment.Assign(symbol.Name, value);
        return value;
    }

    private static object? EvaluateIf(IReadOnlyList<SExpr> items, Environment environment)
    {
        if (items.Count < 3)
        {
            throw new InvalidOperationException("if expects a condition and at least one branch.");
        }

        var condition = Evaluate(items[1], environment);
        var branch = Builtins.IsTruthy(condition)
            ? items[2]
            : items.Count >= 4 ? items[3] : Nil.Instance;

        return Evaluate(branch, environment);
    }

    private static object? EvaluateFunction(IReadOnlyList<SExpr> items, Environment environment)
    {
        if (items.Count != 4 || items[1] is not Symbol name)
        {
            throw new InvalidOperationException("function expects a name, parameter list, and body.");
        }

        var lambda = CreateLambda(items[2], items[3], environment);
        environment.Define(name.Name, lambda);
        return lambda;
    }

    private static object? EvaluateLambda(IReadOnlyList<SExpr> items, Environment environment)
    {
        if (items.Count != 3)
        {
            throw new InvalidOperationException("lambda expects a parameter list and a body.");
        }

        return CreateLambda(items[1], items[2], environment);
    }

    private static object? EvaluateCall(IReadOnlyList<SExpr> items, Environment environment)
    {
        if (items.Count < 2)
        {
            throw new InvalidOperationException("call expects a callee expression.");
        }

        var callee = Evaluate(items[1], environment);
        var arguments = EvaluateArguments(items.Skip(2), environment);
        return Invoke(callee, arguments);
    }

    private static object? EvaluateMember(IReadOnlyList<SExpr> items, Environment environment)
    {
        if (items.Count != 3 || items[2] is not StringLiteral property)
        {
            throw new InvalidOperationException("member expects an object expression and a string literal property.");
        }

        var target = Evaluate(items[1], environment);
        return target switch
        {
            IDictionary<string, object?> dictionary when dictionary.TryGetValue(property.Value, out var value) => value,
            _ => throw new InvalidOperationException($"Cannot read property '{property.Value}' from value '{target}'."),
        };
    }

    private static object? EvaluateReturn(IReadOnlyList<SExpr> items, Environment environment)
    {
        var value = items.Count >= 2 ? Evaluate(items[1], environment) : null;
        throw new ReturnSignal(value);
    }

    private static LambdaValue CreateLambda(SExpr parametersExpr, SExpr body, Environment environment)
    {
        if (parametersExpr is not Cons && parametersExpr is not Nil)
        {
            throw new InvalidOperationException("Lambda parameters must be expressed as a list.");
        }

        var parameters = parametersExpr.AsEnumerable().Select(parameter => parameter switch
        {
            Symbol symbol => symbol.Name,
            _ => throw new InvalidOperationException("Lambda parameters must be symbols."),
        }).ToList();

        return new LambdaValue(parameters, body, environment);
    }

    private static IReadOnlyList<object?> EvaluateArguments(IEnumerable<SExpr> arguments, Environment environment)
        => arguments.Select(argument => Evaluate(argument, environment)).ToList();

    private static object? Invoke(object? callable, IReadOnlyList<object?> arguments)
        => callable switch
        {
            Func<IReadOnlyList<object?>, object?> func => func(arguments),
            LambdaValue lambda => lambda.Invoke(arguments),
            _ => throw new InvalidOperationException($"Value '{callable}' is not callable."),
        };

    internal sealed class ReturnSignal : Exception
    {
        public ReturnSignal(object? value)
        {
            Value = value;
        }

        public object? Value { get; }
    }
}
