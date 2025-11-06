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
}
