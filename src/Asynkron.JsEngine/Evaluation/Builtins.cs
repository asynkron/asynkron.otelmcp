using System;
using System.Collections.Generic;
using System.Linq;

namespace Asynkron.JsEngine.Evaluation;

/// <summary>
/// Provides the built-in functions available to interpreted code.
/// </summary>
internal static class Builtins
{
    public static void Populate(Environment environment)
    {
        environment.Define("+", new Func<IReadOnlyList<object?>, object?>(args => args.Select(ToNumber).Aggregate(0d, (acc, value) => acc + value)));
        environment.Define("-", new Func<IReadOnlyList<object?>, object?>(args =>
        {
            if (args.Count == 0)
            {
                return 0d;
            }

            var numbers = args.Select(ToNumber).ToList();
            var first = numbers[0];
            return args.Count == 1 ? -first : numbers.Skip(1).Aggregate(first, (acc, value) => acc - value);
        }));

        environment.Define("*", new Func<IReadOnlyList<object?>, object?>(args => args.Select(ToNumber).Aggregate(1d, (acc, value) => acc * value)));
        environment.Define("/", new Func<IReadOnlyList<object?>, object?>(args =>
        {
            var numbers = args.Select(ToNumber).ToList();
            if (numbers.Count == 0)
            {
                throw new InvalidOperationException("Division requires at least one operand.");
            }

            var first = numbers[0];
            return numbers.Skip(1).Aggregate(first, (acc, value) => acc / value);
        }));

        environment.Define("%", new Func<IReadOnlyList<object?>, object?>(args =>
        {
            if (args.Count != 2)
            {
                throw new InvalidOperationException("Modulo expects exactly two operands.");
            }

            return ToNumber(args[0]) % ToNumber(args[1]);
        }));

        environment.Define("===", new Func<IReadOnlyList<object?>, object?>(args => Compare(args, (a, b) => Equals(a, b))));
        environment.Define("!==", new Func<IReadOnlyList<object?>, object?>(args => Compare(args, (a, b) => !Equals(a, b))));
        environment.Define("==", new Func<IReadOnlyList<object?>, object?>(args => Compare(args, LooseEqual)));
        environment.Define("!=", new Func<IReadOnlyList<object?>, object?>(args => Compare(args, (a, b) => !LooseEqual(a, b))));
        environment.Define(">", new Func<IReadOnlyList<object?>, object?>(args => Compare(args, (a, b) => ToNumber(a) > ToNumber(b))));
        environment.Define(">=", new Func<IReadOnlyList<object?>, object?>(args => Compare(args, (a, b) => ToNumber(a) >= ToNumber(b))));
        environment.Define("<", new Func<IReadOnlyList<object?>, object?>(args => Compare(args, (a, b) => ToNumber(a) < ToNumber(b))));
        environment.Define("<=", new Func<IReadOnlyList<object?>, object?>(args => Compare(args, (a, b) => ToNumber(a) <= ToNumber(b))));

        environment.Define("and", new Func<IReadOnlyList<object?>, object?>(args =>
        {
            foreach (var arg in args)
            {
                if (!IsTruthy(arg))
                {
                    return arg;
                }
            }

            return args.LastOrDefault();
        }));

        environment.Define("or", new Func<IReadOnlyList<object?>, object?>(args =>
        {
            foreach (var arg in args)
            {
                if (IsTruthy(arg))
                {
                    return arg;
                }
            }

            return args.LastOrDefault();
        }));

        environment.Define("not", new Func<IReadOnlyList<object?>, object?>(args =>
        {
            if (args.Count != 1)
            {
                throw new InvalidOperationException("not expects exactly one argument.");
            }

            return !IsTruthy(args[0]);
        }));

        environment.Define("??", new Func<IReadOnlyList<object?>, object?>(args =>
        {
            foreach (var arg in args)
            {
                if (arg is not null)
                {
                    return arg;
                }
            }

            return null;
        }));
    }

    private static bool Compare(IReadOnlyList<object?> args, Func<object?, object?, bool> comparator)
    {
        if (args.Count != 2)
        {
            throw new InvalidOperationException("Comparison expects exactly two operands.");
        }

        return comparator(args[0], args[1]);
    }

    private static double ToNumber(object? value) => value switch
    {
        null => 0d,
        double d => d,
        float f => f,
        int i => i,
        long l => l,
        bool b => b ? 1d : 0d,
        string s when double.TryParse(s, out var result) => result,
        _ => throw new InvalidOperationException($"Cannot convert value '{value}' to number."),
    };

    private static bool LooseEqual(object? left, object? right)
    {
        if (left is null && right is null)
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        return Equals(left, right);
    }

    internal static bool IsTruthy(object? value) => value switch
    {
        null => false,
        bool b => b,
        double d => Math.Abs(d) > double.Epsilon,
        float f => Math.Abs(f) > float.Epsilon,
        int i => i != 0,
        long l => l != 0,
        string s => !string.IsNullOrEmpty(s),
        _ => true,
    };
}
