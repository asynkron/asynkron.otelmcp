using System;
using System.Globalization;
using System.Linq;
using Asynkron.JsEngine;
using Asynkron.JsEngine.SExpressions;
using Xunit;

namespace Asynkron.JsEngine.Tests;

public class JsExecutionEngineTests
{
    private static readonly JsExecutionEngine Engine = new();

    [Fact]
    public void Parse_LetStatementProducesConsStructure()
    {
        var expression = Engine.Parse("let x = 2 + 3;");
        Assert.Equal("(let x (+ 2 3))", Format(expression));
    }

    [Fact]
    public void Execute_EvaluatesArithmeticExpressions()
    {
        var result = Engine.Execute("let x = 1 + 2; x * 3;");
        Assert.Equal(9d, result);
    }

    [Fact]
    public void Execute_RespectsIfBranches()
    {
        var result = Engine.Execute("let x = 5; if (x > 3) { x = x + 1; } x;");
        Assert.Equal(6d, result);
    }

    [Fact]
    public void Execute_SupportsFunctionDeclarations()
    {
        var result = Engine.Execute("function add(a, b) { return a + b; } add(2, 3);");
        Assert.Equal(5d, result);
    }

    private static string Format(SExpr expression) => expression switch
    {
        Nil => "()",
        Symbol symbol => symbol.Name,
        NumberLiteral number => number.Value.ToString(CultureInfo.InvariantCulture),
        StringLiteral str => $"\"{str.Value}\"",
        BooleanLiteral boolean => boolean.Value ? "true" : "false",
        NullLiteral => "null",
        Cons cons => $"({string.Join(" ", cons.ToList().Select(Format))})",
        _ => throw new InvalidOperationException($"Unsupported expression type {expression.GetType().Name}"),
    };
}
