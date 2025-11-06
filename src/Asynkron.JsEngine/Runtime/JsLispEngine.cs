using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Asynkron.JsEngine.Parsing;
using Asynkron.JsEngine.SExpressions;

namespace Asynkron.JsEngine.Runtime;

/// <summary>
/// Evaluates S-expressions generated from JavaScript source using a small,
/// Lisp-inspired execution model. Only a JavaScript subset is supported,
/// but the design mirrors traditional Lisp interpreters (special forms,
/// lexical environments, and first-class functions).
/// </summary>
public sealed class JsLispEngine
{
    private readonly JsSExpressionBuilder _builder;
    private const double NumberTolerance = 1e-9;

    public JsLispEngine(JsSExpressionBuilder? builder = null)
    {
        _builder = builder ?? new JsSExpressionBuilder();
    }

    /// <summary>
    /// Parses JavaScript and returns the Lisp-style S-expression representation.
    /// </summary>
    public SExpression Parse(string source) => _builder.ParseScript(source);

    /// <summary>
    /// Parses and evaluates JavaScript code inside a fresh environment.
    /// </summary>
    public object? Execute(string source)
    {
        var program = Parse(source);
        return Evaluate(program, CreateDefaultEnvironment());
    }

    /// <summary>
    /// Evaluates an already parsed S-expression.
    /// </summary>
    public object? Evaluate(SExpression expression, JsEnvironment? environment = null)
    {
        environment ??= CreateDefaultEnvironment();
        return EvaluateInternal(expression, environment);
    }

    /// <summary>
    /// Creates a new environment seeded with JavaScript-like built-ins.
    /// </summary>
    public static JsEnvironment CreateDefaultEnvironment()
    {
        var environment = new JsEnvironment();
        environment.Define("+", CreateBuiltin(args =>
        {
            if (args.Any(arg => arg is string or char))
            {
                return string.Concat(args.Select(FormatValue));
            }

            var sum = 0d;
            foreach (var argument in args)
            {
                sum += ToNumber(argument);
            }

            return sum;
        }), isMutable: false);

        environment.Define("-", CreateBuiltin(args =>
        {
            if (args.Count == 0)
            {
                throw new InvalidOperationException("'-' expects at least one argument.");
            }

            var result = ToNumber(args[0]);
            if (args.Count == 1)
            {
                return -result;
            }

            for (var i = 1; i < args.Count; i++)
            {
                result -= ToNumber(args[i]);
            }

            return result;
        }), isMutable: false);

        environment.Define("*", CreateBuiltin(args =>
        {
            var product = 1d;
            foreach (var argument in args)
            {
                product *= ToNumber(argument);
            }

            return product;
        }), isMutable: false);

        environment.Define("/", CreateBuiltin(args =>
        {
            if (args.Count == 0)
            {
                throw new InvalidOperationException("'/' expects at least one argument.");
            }

            var quotient = ToNumber(args[0]);
            for (var i = 1; i < args.Count; i++)
            {
                quotient /= ToNumber(args[i]);
            }

            return quotient;
        }), isMutable: false);

        environment.Define("%", CreateBuiltin(args =>
        {
            if (args.Count != 2)
            {
                throw new InvalidOperationException("'%' expects exactly two arguments.");
            }

            return ToNumber(args[0]) % ToNumber(args[1]);
        }), isMutable: false);

        environment.Define("band", CreateBuiltin(args =>
        {
            EnsureArity(args, 2, "band");
            return ToInt64(args[0]) & ToInt64(args[1]);
        }), isMutable: false);

        environment.Define("bor", CreateBuiltin(args =>
        {
            EnsureArity(args, 2, "bor");
            return ToInt64(args[0]) | ToInt64(args[1]);
        }), isMutable: false);

        environment.Define("bxor", CreateBuiltin(args =>
        {
            EnsureArity(args, 2, "bxor");
            return ToInt64(args[0]) ^ ToInt64(args[1]);
        }), isMutable: false);

        environment.Define("shl", CreateBuiltin(args =>
        {
            EnsureArity(args, 2, "shl");
            return ToInt64(args[0]) << (int)ToInt64(args[1]);
        }), isMutable: false);

        environment.Define("shr", CreateBuiltin(args =>
        {
            EnsureArity(args, 2, "shr");
            return ToInt64(args[0]) >> (int)ToInt64(args[1]);
        }), isMutable: false);

        environment.Define("ushr", CreateBuiltin(args =>
        {
            EnsureArity(args, 2, "ushr");
            return ToUInt32(args[0]) >> (int)ToInt64(args[1]);
        }), isMutable: false);

        environment.Define("bnot", CreateBuiltin(args =>
        {
            EnsureArity(args, 1, "bnot");
            return ~ToInt64(args[0]);
        }), isMutable: false);
        DefineNumericComparison(environment, "<", (a, b) => a < b);
        DefineNumericComparison(environment, "<=", (a, b) => a <= b);
        DefineNumericComparison(environment, ">", (a, b) => a > b);
        DefineNumericComparison(environment, ">=", (a, b) => a >= b);

        environment.Define("==", CreateBuiltin(args =>
        {
            EnsureArity(args, 2, "==");
            return LooseEquals(args[0], args[1]);
        }), isMutable: false);

        environment.Define("!=", CreateBuiltin(args =>
        {
            EnsureArity(args, 2, "!=");
            return !LooseEquals(args[0], args[1]);
        }), isMutable: false);

        environment.Define("===", CreateBuiltin(args =>
        {
            EnsureArity(args, 2, "===");
            return StrictEquals(args[0], args[1]);
        }), isMutable: false);

        environment.Define("!==", CreateBuiltin(args =>
        {
            EnsureArity(args, 2, "!==");
            return !StrictEquals(args[0], args[1]);
        }), isMutable: false);

        environment.Define("pow", CreateBuiltin(args => Math.Pow(ToNumber(args[0]), ToNumber(args[1]))), isMutable: false);
        environment.Define("and", CreateBuiltin(args => args.All(IsTruthy)), isMutable: false);
        environment.Define("or", CreateBuiltin(args => args.Any(IsTruthy)), isMutable: false);
        environment.Define("not", CreateBuiltin(args => args.Count == 1 && !IsTruthy(args[0])), isMutable: false);
        environment.Define("typeof", CreateBuiltin(args => args.Count == 1 ? args[0]?.GetType().Name.ToLowerInvariant() ?? "undefined" : throw new InvalidOperationException("typeof expects exactly one argument.")), isMutable: false);
        environment.Define("undefined", null, isMutable: false);

        // Provide a minimal console implementation for quick experimentation.
        var console = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["log"] = CreateBuiltin(args =>
            {
                Console.WriteLine(string.Join(" ", args.Select(FormatValue)));
                return args.LastOrDefault();
            })
        };
        environment.Define("console", console, isMutable: false);

        return environment;
    }

    private static void RegisterNumericBuiltin(JsEnvironment environment, string name, Func<IReadOnlyList<object?>, double> evaluator)
    {
        environment.Define(name, CreateBuiltin(args => evaluator(args)), isMutable: false);
    }

    private static Func<IReadOnlyList<object?>, object?> CreateBuiltin(Func<IReadOnlyList<object?>, object?> implementation) => implementation;

    private static void EnsureArity(IReadOnlyList<object?> args, int expected, string name)
    {
        if (args.Count != expected)
        {
            throw new InvalidOperationException($"'{name}' expects exactly {expected} arguments but received {args.Count}.");
        }
    }

    private static void DefineNumericComparison(JsEnvironment environment, string name, Func<double, double, bool> comparer)
    {
        environment.Define(name, CreateBuiltin(args =>
        {
            EnsureArity(args, 2, name);
            return comparer(ToNumber(args[0]), ToNumber(args[1]));
        }), isMutable: false);
    }

    private static bool LooseEquals(object? left, object? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        if (IsNumeric(left) || IsNumeric(right))
        {
            return Math.Abs(ToNumber(left) - ToNumber(right)) < NumberTolerance;
        }

        return Equals(left, right);
    }

    private static bool StrictEquals(object? left, object? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        if (left.GetType() != right.GetType())
        {
            return false;
        }

        if (IsNumeric(left))
        {
            return Math.Abs(ToNumber(left) - ToNumber(right)) < NumberTolerance;
        }

        return Equals(left, right);
    }

    private static double ToNumber(object? value)
    {
        return value switch
        {
            null => 0d,
            double d => d,
            float f => f,
            long l => l,
            ulong ul => ul,
            int i => i,
            uint ui => ui,
            short s => s,
            ushort us => us,
            byte b => b,
            sbyte sb => sb,
            decimal dec => (double)dec,
            bool bl => bl ? 1d : 0d,
            string str => double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : throw new InvalidOperationException($"Cannot convert '{str}' to a number."),
            _ => throw new InvalidOperationException($"Unsupported numeric conversion from type '{value.GetType()}'.")
        };
    }

    private static long ToInt64(object? value) => Convert.ToInt64(ToNumber(value));

    private static uint ToUInt32(object? value) => Convert.ToUInt32(ToNumber(value));

    private static object? EvaluateInternal(SExpression expression, JsEnvironment environment)
    {
        return expression switch
        {
            Nil => null,
            Literal literal => literal.Value,
            Symbol symbol => environment.Get(symbol.Name),
            Cons cons => EvaluateCombination(cons, environment),
            _ => throw new InvalidOperationException($"Unsupported S-expression node '{expression}'.")
        };
    }

    private static object? EvaluateCombination(Cons cons, JsEnvironment environment)
    {
        if (cons.Car is Symbol symbol && TryEvaluateSpecialForm(symbol.Name, cons.Cdr, environment, out var result))
        {
            return result;
        }

        var function = EvaluateInternal(cons.Car, environment);
        var arguments = EvaluateArguments(cons.Cdr, environment);
        return Invoke(function, arguments);
    }

    private static bool TryEvaluateSpecialForm(string name, SExpression args, JsEnvironment environment, out object? result)
    {
        switch (name)
        {
            case "begin":
                result = EvaluateBegin(args, environment);
                return true;
            case "let":
            case "var":
                result = EvaluateDeclaration(args, environment, isMutable: true);
                return true;
            case "const":
                result = EvaluateDeclaration(args, environment, isMutable: false);
                return true;
            case "set":
                result = EvaluateAssignment(args, environment);
                return true;
            case "def":
                result = EvaluateDefinition(args, environment);
                return true;
            case "lambda":
                result = CreateLambda(args, environment);
                return true;
            case "if":
                result = EvaluateIf(args, environment);
                return true;
            case "return":
                throw new ReturnSignal(EvaluateReturn(args, environment));
            case "call":
                result = EvaluateCall(args, environment);
                return true;
            case "member":
                result = EvaluateMember(args, environment);
                return true;
            case "post-update":
                result = EvaluatePostUpdate(args, environment);
                return true;
            default:
                result = null;
                return false;
        }
    }

    private static object? EvaluateBegin(SExpression args, JsEnvironment environment)
    {
        object? value = null;
        foreach (var expression in args.ToEnumerable())
        {
            value = EvaluateInternal(expression, environment);
        }

        return value;
    }

    private static object? EvaluateDeclaration(SExpression args, JsEnvironment environment, bool isMutable)
    {
        var items = args.ToImmutableArray();
        if (items.Length is < 1 or > 2)
        {
            throw new InvalidOperationException("Declarations expect a binding and an optional initializer.");
        }

        if (items[0] is not Symbol symbol)
        {
            throw new InvalidOperationException("Expected identifier symbol in declaration.");
        }

        var initializer = items.Length == 2 ? EvaluateInternal(items[1], environment) : null;
        environment.Define(symbol.Name, initializer, isMutable);
        return initializer;
    }

    private static object? EvaluateAssignment(SExpression args, JsEnvironment environment)
    {
        var items = args.ToImmutableArray();
        if (items.Length != 2)
        {
            throw new InvalidOperationException("Assignments expect a target and a value.");
        }

        if (items[0] is Symbol symbol)
        {
            var value = EvaluateInternal(items[1], environment);
            environment.Set(symbol.Name, value);
            return value;
        }

        if (items[0] is Cons member && member.Car is Symbol { Name: "member" })
        {
            return EvaluateMemberAssignment(member.Cdr, items[1], environment);
        }

        throw new InvalidOperationException("Unsupported assignment target.");
    }

    private static object? EvaluateDefinition(SExpression args, JsEnvironment environment)
    {
        var items = args.ToImmutableArray();
        if (items.Length != 2 || items[0] is not Symbol symbol)
        {
            throw new InvalidOperationException("Function definitions require a name and body.");
        }

        var value = EvaluateInternal(items[1], environment);
        environment.Define(symbol.Name, value, isMutable: false);
        return value;
    }

    private static object? CreateLambda(SExpression args, JsEnvironment environment)
    {
        var items = args.ToImmutableArray();
        if (items.Length == 0)
        {
            throw new InvalidOperationException("Lambda requires a parameter list.");
        }

        if (items[0] is not SExpression parameterList)
        {
            throw new InvalidOperationException("Expected parameter list as the first lambda argument.");
        }

        var parameters = parameterList.ToEnumerable().Select(expr => expr switch
        {
            Symbol symbol => symbol.Name,
            _ => throw new InvalidOperationException("Lambda parameters must be identifiers.")
        }).ToArray();

        var body = items.Length > 1
            ? items.Skip(1).ToArray()
            : new[] { (SExpression)SExpr.Nil };

        return new JsFunction(parameters, body, environment);
    }

    private static object? EvaluateIf(SExpression args, JsEnvironment environment)
    {
        var items = args.ToImmutableArray();
        if (items.Length is < 2 or > 3)
        {
            throw new InvalidOperationException("if expects a test, consequent, and optional alternate.");
        }

        var test = EvaluateInternal(items[0], environment);
        if (IsTruthy(test))
        {
            return EvaluateInternal(items[1], environment);
        }

        return items.Length == 3 ? EvaluateInternal(items[2], environment) : null;
    }

    private static object? EvaluateReturn(SExpression args, JsEnvironment environment)
    {
        if (SExpr.IsNil(args))
        {
            return null;
        }

        var items = args.ToImmutableArray();
        return items.Length == 0 ? null : EvaluateInternal(items[0], environment);
    }

    private static object? EvaluateCall(SExpression args, JsEnvironment environment)
    {
        var items = args.ToImmutableArray();
        if (items.Length == 0)
        {
            throw new InvalidOperationException("call expects a callee expression.");
        }

        var callee = EvaluateInternal(items[0], environment);
        var arguments = items.Skip(1).Select(item => EvaluateInternal(item, environment)).ToArray();
        return Invoke(callee, arguments);
    }

    private static object? EvaluateMember(SExpression args, JsEnvironment environment)
    {
        var items = args.ToImmutableArray();
        if (items.Length != 2)
        {
            throw new InvalidOperationException("member expects a target and a property.");
        }

        var target = EvaluateInternal(items[0], environment);
        var property = items[1] switch
        {
            Symbol symbol => symbol.Name,
            _ => EvaluateInternal(items[1], environment)
        };

        return ResolveMember(target, property);
    }

    private static object? EvaluateMemberAssignment(SExpression memberArgs, SExpression valueExpression, JsEnvironment environment)
    {
        var args = memberArgs.ToImmutableArray();
        if (args.Length != 2)
        {
            throw new InvalidOperationException("member assignment expects target and property.");
        }

        var target = EvaluateInternal(args[0], environment);
        var property = args[1] switch
        {
            Symbol symbol => symbol.Name,
            _ => EvaluateInternal(args[1], environment)
        };

        var value = EvaluateInternal(valueExpression, environment);
        return AssignMember(target, property, value);
    }

    private static object? EvaluatePostUpdate(SExpression args, JsEnvironment environment)
    {
        var items = args.ToImmutableArray();
        if (items.Length != 2 || items[0] is not Symbol symbol)
        {
            throw new InvalidOperationException("post-update expects an identifier and the updated expression.");
        }

        var original = environment.Get(symbol.Name);
        var updated = EvaluateInternal(items[1], environment);
        environment.Set(symbol.Name, updated);
        return original;
    }

    private static object? ResolveMember(object? target, object? property)
    {
        if (target is null)
        {
            throw new InvalidOperationException("Cannot access members on null.");
        }

        if (target is IDictionary<string, object?> dictionary && property is string key)
        {
            return dictionary.TryGetValue(key, out var value) ? value : null;
        }

        var type = target.GetType();
        if (property is string propertyName)
        {
            var propertyInfo = type.GetProperty(propertyName);
            if (propertyInfo is not null)
            {
                return propertyInfo.GetValue(target);
            }
        }

        throw new InvalidOperationException($"Cannot resolve member '{property}'.");
    }

    private static object? AssignMember(object? target, object? property, object? value)
    {
        if (target is IDictionary<string, object?> dictionary && property is string key)
        {
            dictionary[key] = value;
            return value;
        }

        if (target is not null && property is string propertyName)
        {
            var propertyInfo = target.GetType().GetProperty(propertyName);
            if (propertyInfo is not null)
            {
                propertyInfo.SetValue(target, value);
                return value;
            }
        }

        throw new InvalidOperationException("Unsupported member assignment target.");
    }

    private static IReadOnlyList<object?> EvaluateArguments(SExpression expression, JsEnvironment environment)
    {
        return expression.ToEnumerable().Select(arg => EvaluateInternal(arg, environment)).ToArray();
    }

    private static object? Invoke(object? callee, IReadOnlyList<object?> arguments)
    {
        return callee switch
        {
            JsFunction function => function.Invoke(arguments),
            Func<IReadOnlyList<object?>, object?> builtin => builtin(arguments),
            Delegate delegateFunc => delegateFunc.DynamicInvoke(arguments.Select(a => a).ToArray()),
            _ => throw new InvalidOperationException("Attempted to call a non-callable value.")
        };
    }

    private static bool IsTruthy(object? value)
    {
        return value switch
        {
            null => false,
            bool b => b,
            double d => Math.Abs(d) > double.Epsilon,
            float f => Math.Abs(f) > float.Epsilon,
            int i => i != 0,
            long l => l != 0,
            string s => s.Length > 0,
            _ => true
        };
    }

    private static bool IsNumeric(object? value)
    {
        return value is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal;
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            null => "null",
            bool b => b ? "true" : "false",
            double d => d.ToString(CultureInfo.InvariantCulture),
            float f => f.ToString(CultureInfo.InvariantCulture),
            _ => value?.ToString() ?? string.Empty
        };
    }

    private sealed class JsFunction
    {
        private readonly IReadOnlyList<string> _parameters;
        private readonly IReadOnlyList<SExpression> _body;
        private readonly JsEnvironment _closure;

        public JsFunction(IReadOnlyList<string> parameters, IReadOnlyList<SExpression> body, JsEnvironment closure)
        {
            _parameters = parameters;
            _body = body;
            _closure = closure;
        }

        public object? Invoke(IReadOnlyList<object?> arguments)
        {
            var invocationEnvironment = new JsEnvironment(_closure);
            for (var i = 0; i < _parameters.Count; i++)
            {
                var value = i < arguments.Count ? arguments[i] : null;
                invocationEnvironment.Define(_parameters[i], value);
            }

            try
            {
                object? result = null;
                foreach (var expression in _body)
                {
                    result = EvaluateInternal(expression, invocationEnvironment);
                }

                return result;
            }
            catch (ReturnSignal signal)
            {
                return signal.Value;
            }
        }
    }

    private sealed class ReturnSignal : Exception
    {
        public ReturnSignal(object? value)
        {
            Value = value;
        }

        public object? Value { get; }
    }
}
