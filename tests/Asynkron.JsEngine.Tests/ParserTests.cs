using Asynkron.JsEngine;

namespace Asynkron.JsEngine.Tests;

public class ParserTests
{
    [Fact]
    public void ParseLetDeclarationProducesExpectedSExpression()
    {
        var engine = new JsEngine();
        var program = engine.Parse("let answer = 1 + 2; answer;");

        Assert.Same(JsSymbols.Program, program.Head);
        var letStatement = Assert.IsType<Cons>(program.Rest.Head);
        Assert.Same(JsSymbols.Let, letStatement.Head);
        Assert.Equal(Symbol.Intern("answer"), letStatement.Rest.Head);

        var addition = Assert.IsType<Cons>(letStatement.Rest.Rest.Head);
        Assert.Equal(Symbol.Intern("+"), addition.Head);
        Assert.Equal(1d, addition.Rest.Head);
        Assert.Equal(2d, addition.Rest.Rest.Head);

        var expressionStatement = Assert.IsType<Cons>(program.Rest.Rest.Head);
        Assert.Same(JsSymbols.ExpressionStatement, expressionStatement.Head);
        Assert.Equal(Symbol.Intern("answer"), expressionStatement.Rest.Head);
    }

    [Fact]
    public void ParseObjectLiteralAndPropertyAccess()
    {
        var engine = new JsEngine();
        var program = engine.Parse("let obj = { a: 10, x: function () { return 42; } }; obj.a;");

        var letStatement = Assert.IsType<Cons>(program.Rest.Head);
        Assert.Same(JsSymbols.Let, letStatement.Head);

        var objectLiteral = Assert.IsType<Cons>(letStatement.Rest.Rest.Head);
        Assert.Same(JsSymbols.ObjectLiteral, objectLiteral.Head);

        var firstProperty = Assert.IsType<Cons>(objectLiteral.Rest.Head);
        Assert.Same(JsSymbols.Property, firstProperty.Head);
        Assert.Equal("a", firstProperty.Rest.Head);
        Assert.Equal(10d, firstProperty.Rest.Rest.Head);

        var secondProperty = Assert.IsType<Cons>(objectLiteral.Rest.Rest.Head);
        Assert.Same(JsSymbols.Property, secondProperty.Head);
        Assert.Equal("x", secondProperty.Rest.Head);
        var functionExpression = Assert.IsType<Cons>(secondProperty.Rest.Rest.Head);
        Assert.Same(JsSymbols.Lambda, functionExpression.Head); // ensure the function value stays a lambda expression

        var expressionStatement = Assert.IsType<Cons>(program.Rest.Rest.Head);
        Assert.Same(JsSymbols.ExpressionStatement, expressionStatement.Head);

        var propertyAccess = Assert.IsType<Cons>(expressionStatement.Rest.Head);
        Assert.Same(JsSymbols.GetProperty, propertyAccess.Head);
        Assert.Equal(Symbol.Intern("obj"), propertyAccess.Rest.Head);
        Assert.Equal("a", propertyAccess.Rest.Rest.Head);
    }
}
