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

        if (ReferenceEquals(symbol, JsSymbols.Class))
        {
            return EvaluateClass(cons, environment);
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

    private static object? EvaluateClass(Cons cons, Environment environment)
    {
        var name = ExpectSymbol(cons.Rest.Head, "Expected class name symbol.");
        var constructorExpression = cons.Rest.Rest.Head;
        var methodsList = ExpectCons(cons.Rest.Rest.Rest.Head, "Expected class body list.");

        var constructorValue = EvaluateExpression(constructorExpression, environment);
        if (constructorValue is not JsFunction constructor)
        {
            throw new InvalidOperationException("Class constructor must be a function.");
        }

        environment.Define(name, constructor);

        if (!constructor.TryGetProperty("prototype", out var prototypeValue) || prototypeValue is not JsObject prototype)
        {
            prototype = new JsObject();
            constructor.SetProperty("prototype", prototype);
        }

        prototype.SetProperty("constructor", constructor);

        foreach (var methodExpression in methodsList)
        {
            var methodCons = ExpectCons(methodExpression, "Expected method definition.");
            var tag = ExpectSymbol(methodCons.Head, "Expected method tag.");
            if (!ReferenceEquals(tag, JsSymbols.Method))
            {
                throw new InvalidOperationException("Invalid entry in class body.");
            }

            var methodName = methodCons.Rest.Head as string
                ?? throw new InvalidOperationException("Expected method name.");
            var functionExpression = methodCons.Rest.Rest.Head;
            var methodValue = EvaluateExpression(functionExpression, environment);

            if (methodValue is not IJsCallable)
            {
                throw new InvalidOperationException($"Class method '{methodName}' must be callable.");
            }

            prototype.SetProperty(methodName, methodValue);
        }

        return constructor;
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

        if (ReferenceEquals(symbol, JsSymbols.ObjectLiteral))
        {
            return EvaluateObjectLiteral(cons, environment);
        }

        if (ReferenceEquals(symbol, JsSymbols.GetProperty))
        {
            return EvaluateGetProperty(cons, environment);
        }

        if (ReferenceEquals(symbol, JsSymbols.SetProperty))
        {
            return EvaluateSetProperty(cons, environment);
        }

        if (ReferenceEquals(symbol, JsSymbols.New))
        {
            return EvaluateNew(cons, environment);
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
        var (callee, thisValue) = ResolveCallee(calleeExpression, environment);
        if (callee is not IJsCallable callable)
        {
            throw new InvalidOperationException("Attempted to call a non-callable value.");
        }

        var arguments = new List<object?>();
        foreach (var argumentExpression in cons.Rest.Rest)
        {
            arguments.Add(EvaluateExpression(argumentExpression, environment));
        }

        return callable.Invoke(arguments, thisValue);
    }

    private static (object? Callee, object? ThisValue) ResolveCallee(object? calleeExpression, Environment environment)
    {
        if (calleeExpression is Cons { Head: Symbol { } head } propertyCons && ReferenceEquals(head, JsSymbols.GetProperty))
        {
            var targetExpression = propertyCons.Rest.Head;
            var propertyName = propertyCons.Rest.Rest.Head as string
                ?? throw new InvalidOperationException("Property access requires a string name.");

            var target = EvaluateExpression(targetExpression, environment);
            if (TryGetPropertyValue(target, propertyName, out var value))
            {
                return (value, target);
            }

            return (null, target);
        }

        return (EvaluateExpression(calleeExpression, environment), null);
    }

    private static object EvaluateObjectLiteral(Cons cons, Environment environment)
    {
        var result = new JsObject();
        foreach (var propertyExpression in cons.Rest)
        {
            var propertyCons = ExpectCons(propertyExpression, "Expected property description in object literal.");
            if (propertyCons.Head is not Symbol { } propertyTag || !ReferenceEquals(propertyTag, JsSymbols.Property))
            {
                throw new InvalidOperationException("Object literal entries must begin with the 'prop' symbol.");
            }

            var propertyName = propertyCons.Rest.Head as string
                ?? throw new InvalidOperationException("Object literal property name must be a string.");

            var valueExpression = propertyCons.Rest.Rest.Head;
            var value = EvaluateExpression(valueExpression, environment);
            result.SetProperty(propertyName, value);
        }

        return result;
    }

    private static object? EvaluateGetProperty(Cons cons, Environment environment)
    {
        var targetExpression = cons.Rest.Head;
        var propertyName = cons.Rest.Rest.Head as string
            ?? throw new InvalidOperationException("Property access requires a string name.");

        var target = EvaluateExpression(targetExpression, environment);
        if (TryGetPropertyValue(target, propertyName, out var value))
        {
            return value;
        }

        throw new InvalidOperationException($"Cannot read property '{propertyName}' from value '{target}'.");
    }

    private static object? EvaluateSetProperty(Cons cons, Environment environment)
    {
        var targetExpression = cons.Rest.Head;
        var propertyName = cons.Rest.Rest.Head as string
            ?? throw new InvalidOperationException("Property assignment requires a string name.");

        var valueExpression = cons.Rest.Rest.Rest.Head;
        var target = EvaluateExpression(targetExpression, environment);
        var value = EvaluateExpression(valueExpression, environment);
        AssignPropertyValue(target, propertyName, value);
        return value;
    }

    private static object? EvaluateNew(Cons cons, Environment environment)
    {
        var constructorExpression = cons.Rest.Head;
        var constructor = EvaluateExpression(constructorExpression, environment);
        if (constructor is not IJsCallable callable)
        {
            throw new InvalidOperationException("Attempted to construct with a non-callable value.");
        }

        var instance = new JsObject();
        if (TryGetPropertyValue(constructor, "prototype", out var prototype) && prototype is JsObject prototypeObject)
        {
            instance.SetPrototype(prototypeObject);
        }

        var arguments = new List<object?>();
        foreach (var argumentExpression in cons.Rest.Rest)
        {
            arguments.Add(EvaluateExpression(argumentExpression, environment));
        }

        var result = callable.Invoke(arguments, instance);
        return result switch
        {
            JsObject jsObject => jsObject,
            IDictionary<string, object?> dictionary => dictionary,
            _ => instance
        };
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

    private static bool TryGetPropertyValue(object? target, string propertyName, out object? value)
    {
        switch (target)
        {
            case JsObject jsObject when jsObject.TryGetProperty(propertyName, out value):
                return true;
            case JsFunction function when function.TryGetProperty(propertyName, out value):
                return true;
            case HostFunction hostFunction when hostFunction.TryGetProperty(propertyName, out value):
                return true;
            case IDictionary<string, object?> dictionary when dictionary.TryGetValue(propertyName, out value):
                return true;
        }

        value = null;
        return false;
    }

    private static void AssignPropertyValue(object? target, string propertyName, object? value)
    {
        switch (target)
        {
            case JsObject jsObject:
                jsObject.SetProperty(propertyName, value);
                break;
            case JsFunction function:
                function.SetProperty(propertyName, value);
                break;
            case HostFunction hostFunction:
                hostFunction.SetProperty(propertyName, value);
                break;
            case IDictionary<string, object?> dictionary:
                dictionary[propertyName] = value;
                break;
            default:
                throw new InvalidOperationException($"Cannot assign property '{propertyName}' on value '{target}'.");
        }
    }
}
