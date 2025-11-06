using System.Collections.Immutable;
using System.Linq;

namespace Asynkron.JsEngine.SExpressions;

/// <summary>
/// Base type for all S-expressions emitted by the JavaScript parser.
/// </summary>
public abstract record SExpression
{
    private protected SExpression()
    {
    }

    /// <summary>
    /// True when the expression represents the canonical empty list.
    /// </summary>
    public virtual bool IsNil => false;
}

/// <summary>
/// The canonical empty list ("nil").
/// </summary>
public sealed record Nil : SExpression
{
    private Nil()
    {
    }

    public static Nil Instance { get; } = new();

    public override bool IsNil => true;
}

/// <summary>
/// Symbol atoms are used for identifiers and keywords.
/// </summary>
/// <param name="Name">Symbol name.</param>
public sealed record Symbol(string Name) : SExpression
{
    public override string ToString() => Name;
}

/// <summary>
/// Literal atoms wrap raw CLR values such as numbers, booleans or strings.
/// </summary>
/// <param name="Value">The literal value.</param>
public sealed record Literal(object? Value) : SExpression
{
    public override string ToString() => Value switch
    {
        null => "null",
        string s => $"\"{s}\"",
        _ => Value.ToString() ?? string.Empty
    };
}

/// <summary>
/// Cons cells stitch atoms and other cons cells into traditional lists.
/// </summary>
/// <param name="Car">The head element.</param>
/// <param name="Cdr">The tail list.</param>
public sealed record Cons(SExpression Car, SExpression Cdr) : SExpression
{
    public override string ToString()
    {
        return $"({string.Join(" ", this.ToEnumerable().Select(x => x.ToString()))})";
    }
}

/// <summary>
/// Helper utilities for building S-expressions.
/// </summary>
public static class SExpr
{
    public static Nil Nil => Nil.Instance;

    public static Symbol Symbol(string name) => new(name);

    public static Literal Literal(object? value) => new(value);

    public static Cons Cons(SExpression car, SExpression cdr) => new(car, cdr);

    public static SExpression List(params SExpression[] items) => FromEnumerable(items);

    public static SExpression FromEnumerable(IEnumerable<SExpression> items)
    {
        var array = items as SExpression[] ?? items.ToArray();
        var current = (SExpression)Nil.Instance;
        for (var i = array.Length - 1; i >= 0; i--)
        {
            current = new Cons(array[i], current);
        }

        return current;
    }

    public static bool IsNil(SExpression expression) => expression is Nil || expression.IsNil;
}

/// <summary>
/// Convenience extensions for navigating cons lists.
/// </summary>
public static class SExpressionExtensions
{
    /// <summary>
    /// Enumerates a cons list, ensuring that it is a proper list (i.e. the tail ends with nil).
    /// </summary>
    public static IEnumerable<SExpression> ToEnumerable(this SExpression expression)
    {
        var current = expression;
        while (current is Cons cons)
        {
            yield return cons.Car;
            current = cons.Cdr;
        }

        if (!SExpr.IsNil(current))
        {
            throw new InvalidOperationException("Encountered an improper list; dotted pairs are not supported in this context.");
        }
    }

    /// <summary>
    /// Converts a cons list to an immutable array.
    /// </summary>
    public static ImmutableArray<SExpression> ToImmutableArray(this SExpression expression)
    {
        return expression.ToEnumerable().ToImmutableArray();
    }

    /// <summary>
    /// Returns the length of a proper list.
    /// </summary>
    public static int Length(this SExpression expression)
    {
        var count = 0;
        var current = expression;
        while (current is Cons cons)
        {
            count++;
            current = cons.Cdr;
        }

        if (!SExpr.IsNil(current))
        {
            throw new InvalidOperationException("Encountered an improper list; dotted pairs are not supported in this context.");
        }

        return count;
    }
}
