using System.Globalization;

namespace Asynkron.JsEngine;

internal static class Evaluator
{
    public static object? EvaluateProgram(Cons program, Environment environment)
    {
        if (program.IsEmpty || program.Head is not Symbol { } tag || !ReferenceEquals(tag, JsSymbols.Program))
        {
            throw new InvalidOperationException("Program S-expression must start with the 'program' symbol.");
        }

        object? result = null;
        foreach (var statement in program.Rest)
        {
            result = EvaluateStatement(statement, environment);
        }

        return result;
    }

    public static object? EvaluateBlock(Cons block, Environment environment)
    {
        if (block.IsEmpty || block.Head is not Symbol { } tag || !ReferenceEquals(tag, JsSymbols.Block))
        {
            throw new InvalidOperationException("Block S-expression must start with the 'block' symbol.");
        }

        var scope = new Environment(environment);
        object? result = null;
        foreach (var statement in block.Rest)
        {
            result = EvaluateStatement(statement, scope);
        }

        return result;
    }

    private static object? EvaluateStatement(object? statement, Environment environment)
    {
        if (statement is not Cons cons)
        {
            return statement;
        }

        if (cons.Head is not Symbol symbol)
        {
            throw new InvalidOperationException("Statement must start with a symbol.");
        }

        if (ReferenceEquals(symbol, JsSymbols.Let))
        {
            return EvaluateLet(cons, environment);
        }

        if (ReferenceEquals(symbol, JsSymbols.Function))
        {
            return EvaluateFunctionDeclaration(cons, environment);
        }

        if (ReferenceEquals(symbol, JsSymbols.Return))
        {
            return EvaluateReturn(cons, environment);
        }

        if (ReferenceEquals(symbol, JsSymbols.ExpressionStatement))
        {
            var expression = cons.Rest.Head;
            return EvaluateExpression(expression, environment);
        }

        if (ReferenceEquals(symbol, JsSymbols.Block))
        {
            return EvaluateBlock(cons, environment);
        }

        return EvaluateExpression(cons, environment);
    }

    private static object? EvaluateLet(Cons cons, Environment environment)
    {
        var name = ExpectSymbol(cons.Rest.Head, "Expected identifier in let declaration.");
        var valueExpression = cons.Rest.Rest.Head;
        var value = EvaluateExpression(valueExpression, environment);
        environment.Define(name, value);
        return value;
    }

    private static object? EvaluateFunctionDeclaration(Cons cons, Environment environment)
    {
        var name = ExpectSymbol(cons.Rest.Head, "Expected function name.");
        var parameters = ExpectCons(cons.Rest.Rest.Head, "Expected parameter list for function.");
        var body = ExpectCons(cons.Rest.Rest.Rest.Head, "Expected function body block.");
        var function = new JsFunction(name, ToSymbolList(parameters), body, environment);
        environment.Define(name, function);
        return function;
    }

    private static object? EvaluateReturn(Cons cons, Environment environment)
    {
        if (cons.Rest.IsEmpty)
        {
            throw new ReturnSignal(null);
        }

        var value = EvaluateExpression(cons.Rest.Head, environment);
        throw new ReturnSignal(value);
    }

    private static object? EvaluateExpression(object? expression, Environment environment)
    {
        switch (expression)
        {
            case null:
                return null;
            case bool b:
                return b;
            case string s:
                return s;
            case double d:
                return d;
            case Symbol symbol:
                return environment.Get(symbol);
            case Cons cons:
                return EvaluateCompositeExpression(cons, environment);
            default:
                return expression;
        }
    }

    private static object? EvaluateCompositeExpression(Cons cons, Environment environment)
    {
        if (cons.Head is not Symbol symbol)
        {
            throw new InvalidOperationException("Composite expression must begin with a symbol.");
        }

        if (ReferenceEquals(symbol, JsSymbols.Assign))
        {
            var target = ExpectSymbol(cons.Rest.Head, "Expected assignment target.");
            var valueExpression = cons.Rest.Rest.Head;
            var value = EvaluateExpression(valueExpression, environment);
            environment.Assign(target, value);
            return value;
        }

        if (ReferenceEquals(symbol, JsSymbols.Call))
        {
            return EvaluateCall(cons, environment);
        }

        if (ReferenceEquals(symbol, JsSymbols.Negate))
        {
            var operand = EvaluateExpression(cons.Rest.Head, environment);
            return -ToNumber(operand);
        }

        if (ReferenceEquals(symbol, JsSymbols.Not))
        {
            var operand = EvaluateExpression(cons.Rest.Head, environment);
            return !IsTruthy(operand);
        }

        if (ReferenceEquals(symbol, JsSymbols.Lambda))
        {
            var maybeName = cons.Rest.Head as Symbol;
            var parameters = ExpectCons(cons.Rest.Rest.Head, "Expected lambda parameters list.");
            var body = ExpectCons(cons.Rest.Rest.Rest.Head, "Expected lambda body block.");
            return new JsFunction(maybeName, ToSymbolList(parameters), body, environment);
        }

        return EvaluateBinary(cons, environment, symbol);
    }

    private static object? EvaluateCall(Cons cons, Environment environment)
    {
        var calleeExpression = cons.Rest.Head;
        var callee = EvaluateExpression(calleeExpression, environment);
        if (callee is not IJsCallable callable)
        {
            throw new InvalidOperationException("Attempted to call a non-callable value.");
        }

        var arguments = new List<object?>();
        foreach (var argumentExpression in cons.Rest.Rest)
        {
            arguments.Add(EvaluateExpression(argumentExpression, environment));
        }

        return callable.Invoke(arguments);
    }

    private static object? EvaluateBinary(Cons cons, Environment environment, Symbol operatorSymbol)
    {
        var leftExpression = cons.Rest.Head;
        var rightExpression = cons.Rest.Rest.Head;
        var left = EvaluateExpression(leftExpression, environment);
        var right = EvaluateExpression(rightExpression, environment);

        return operatorSymbol.Name switch
        {
            "+" => Add(left, right),
            "-" => ToNumber(left) - ToNumber(right),
            "*" => ToNumber(left) * ToNumber(right),
            "/" => ToNumber(right) == 0 ? throw new DivideByZeroException() : ToNumber(left) / ToNumber(right),
            "==" => Equals(left, right),
            "!=" => !Equals(left, right),
            ">" => ToNumber(left) > ToNumber(right),
            ">=" => ToNumber(left) >= ToNumber(right),
            "<" => ToNumber(left) < ToNumber(right),
            "<=" => ToNumber(left) <= ToNumber(right),
            _ => throw new InvalidOperationException($"Unsupported operator '{operatorSymbol.Name}'.")
        };
    }

    private static IReadOnlyList<Symbol> ToSymbolList(Cons list)
    {
        var result = new List<Symbol>();
        foreach (var item in list)
        {
            result.Add(ExpectSymbol(item, "Expected symbol in parameter list."));
        }

        return result;
    }

    private static Symbol ExpectSymbol(object? value, string message)
        => value is Symbol symbol ? symbol : throw new InvalidOperationException(message);

    private static Cons ExpectCons(object? value, string message)
        => value is Cons cons ? cons : throw new InvalidOperationException(message);

    private static bool IsTruthy(object? value) => value switch
    {
        null => false,
        bool b => b,
        double d => Math.Abs(d) > double.Epsilon,
        string s => s.Length > 0,
        _ => true
    };

    private static double ToNumber(object? value) => value switch
    {
        null => 0,
        double d => d,
        int i => i,
        long l => l,
        bool b => b ? 1 : 0,
        string s when double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) => parsed,
        _ => throw new InvalidOperationException($"Cannot convert value '{value}' to a number.")
    };

    private static object Add(object? left, object? right)
    {
        if (left is string || right is string)
        {
            return ToDisplayString(left) + ToDisplayString(right);
        }

        return ToNumber(left) + ToNumber(right);
    }

    private static string ToDisplayString(object? value) => value switch
    {
        null => "null",
        bool b => b ? "true" : "false",
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
    };
}
