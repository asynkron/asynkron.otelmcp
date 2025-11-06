using System;
using Asynkron.JsEngine.Parsing;
using Asynkron.JsEngine.Runtime;
using Xunit;

namespace Asynkron.JsEngine.Tests;

public class JsLispEngineTests
{
    [Fact]
    public void Parser_ProducesReadableSExpression()
    {
        var builder = new JsSExpressionBuilder();
        var expression = builder.ParseScript("let x = 1 + 2; x * 3;");

        Assert.Equal("(begin (let x (+ 1 2)) (* x 3))", expression.ToString());
    }

    [Fact]
    public void Execute_ComputesFunctionResult()
    {
        var engine = new JsLispEngine();
        var source = @"function double(x) { return x * 2; }
let value = double(21);
value;";

        var result = engine.Execute(source);

        Assert.Equal(42d, result);
    }

    [Fact]
    public void Execute_SupportsConsoleLogging()
    {
        var engine = new JsLispEngine();
        var program = @"const makeGreeter = (name) => {
    return function (input) {
        console.log(name + ': ' + input);
        return name + ' says hi to ' + input;
    };
};

const greeter = makeGreeter('TraceLens');
const result = greeter('Operator');
result;";

        var value = engine.Execute(program);

        Assert.Equal("TraceLens says hi to Operator", value);
    }

    [Fact]
    public void Execute_RespectsConstAssignments()
    {
        var engine = new JsLispEngine();
        var program = "const answer = 41; answer = answer + 1;";

        var exception = Assert.Throws<InvalidOperationException>(() => engine.Execute(program));
        Assert.Contains("Cannot assign to constant", exception.Message);
    }

    [Fact]
    public void Execute_EvaluatesIfStatements()
    {
        var engine = new JsLispEngine();
        var program = @"let x = 10;
if (x > 5) {
    x = x + 5;
} else {
    x = 0;
}
x;";

        var result = engine.Execute(program);

        Assert.Equal(15d, result);
    }
}
