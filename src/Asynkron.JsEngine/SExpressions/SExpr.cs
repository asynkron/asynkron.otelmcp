using System.Collections.Generic;
using System.Linq;

namespace Asynkron.JsEngine.SExpressions;

/// <summary>
/// Base type for all S-expressions produced by the parser.
/// </summary>
public abstract record SExpr
{
    /// <summary>
    /// Creates a cons list from the provided elements.
    /// </summary>
    public static SExpr List(params SExpr[] elements) => List((IEnumerable<SExpr>)elements);

    /// <summary>
    /// Creates a cons list from the provided elements.
    /// </summary>
    public static SExpr List(IEnumerable<SExpr> elements)
    {
        var items = elements.Reverse().ToArray();
        SExpr current = Nil.Instance;
        foreach (var item in items)
        {
            current = new Cons(item, current);
        }

        return current;
    }

    /// <summary>
    /// Converts the current S-expression into an enumerable sequence when it represents a proper list.
    /// </summary>
    public IEnumerable<SExpr> AsEnumerable()
    {
        var current = this;
        while (current is Cons cons)
        {
            yield return cons.Head;
            current = cons.Tail;
        }
    }

    /// <summary>
    /// Enumerates the S-expression as a proper list.
    /// </summary>
    public IReadOnlyList<SExpr> ToList() => AsEnumerable().ToList();
}

/// <summary>
/// Represents the empty list / nil in the cons-based representation.
/// </summary>
public sealed record Nil : SExpr
{
    private Nil()
    {
    }

    public static Nil Instance { get; } = new();
}

/// <summary>
/// Represents a cons cell with a head and tail.
/// </summary>
/// <param name="Head">The first element of the cell.</param>
/// <param name="Tail">The remainder of the list.</param>
public sealed record Cons(SExpr Head, SExpr Tail) : SExpr;

/// <summary>
/// Symbol literal value.
/// </summary>
/// <param name="Name">The symbol name.</param>
public sealed record Symbol(string Name) : SExpr;

/// <summary>
/// Numeric literal.
/// </summary>
/// <param name="Value">The literal numeric value.</param>
public sealed record NumberLiteral(double Value) : SExpr;

/// <summary>
/// String literal.
/// </summary>
/// <param name="Value">The literal string value.</param>
public sealed record StringLiteral(string Value) : SExpr;

/// <summary>
/// Boolean literal.
/// </summary>
/// <param name="Value">The literal boolean value.</param>
public sealed record BooleanLiteral(bool Value) : SExpr;

/// <summary>
/// Represents the JavaScript <c>null</c> literal.
/// </summary>
public sealed record NullLiteral() : SExpr;
