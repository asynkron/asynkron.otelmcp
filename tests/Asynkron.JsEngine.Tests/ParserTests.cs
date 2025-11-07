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
        var program = engine.Parse("let obj = { a: 10, x: function () { return this.x; } }; obj.a;");

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

        Assert.Null(functionExpression.Rest.Head); // anonymous function keeps null name slot
        var body = Assert.IsType<Cons>(functionExpression.Rest.Rest.Rest.Head);
        Assert.Same(JsSymbols.Block, body.Head);
        var returnStatement = Assert.IsType<Cons>(body.Rest.Head);
        Assert.Same(JsSymbols.Return, returnStatement.Head);
        var propertyAccessInReturn = Assert.IsType<Cons>(returnStatement.Rest.Head);
        Assert.Same(JsSymbols.GetProperty, propertyAccessInReturn.Head);
        Assert.Same(JsSymbols.This, propertyAccessInReturn.Rest.Head);
        Assert.Equal("x", propertyAccessInReturn.Rest.Rest.Head);

        var expressionStatement = Assert.IsType<Cons>(program.Rest.Rest.Head);
        Assert.Same(JsSymbols.ExpressionStatement, expressionStatement.Head);

        var propertyAccess = Assert.IsType<Cons>(expressionStatement.Rest.Head);
        Assert.Same(JsSymbols.GetProperty, propertyAccess.Head);
        Assert.Equal(Symbol.Intern("obj"), propertyAccess.Rest.Head);
        Assert.Equal("a", propertyAccess.Rest.Rest.Head);
    }

    [Fact]
    public void ParsePropertyAssignment()
    {
        var engine = new JsEngine();
        var program = engine.Parse("let obj = {}; obj.value = 5;");

        var expressionStatement = Assert.IsType<Cons>(program.Rest.Rest.Head);
        Assert.Same(JsSymbols.ExpressionStatement, expressionStatement.Head);

        var assignment = Assert.IsType<Cons>(expressionStatement.Rest.Head);
        Assert.Same(JsSymbols.SetProperty, assignment.Head);
        Assert.Equal(Symbol.Intern("obj"), assignment.Rest.Head);
        Assert.Equal("value", assignment.Rest.Rest.Head);
        Assert.Equal(5d, assignment.Rest.Rest.Rest.Head);
    }

    [Fact]
    public void ParseNewExpression()
    {
        var engine = new JsEngine();
        var program = engine.Parse("let instance = new Factory.Builder(1, 2); instance;");

        var letStatement = Assert.IsType<Cons>(program.Rest.Head);
        Assert.Same(JsSymbols.Let, letStatement.Head);
        Assert.Equal(Symbol.Intern("instance"), letStatement.Rest.Head);

        var newExpression = Assert.IsType<Cons>(letStatement.Rest.Rest.Head);
        Assert.Same(JsSymbols.New, newExpression.Head);

        var constructor = Assert.IsType<Cons>(newExpression.Rest.Head);
        Assert.Same(JsSymbols.GetProperty, constructor.Head);
        Assert.Equal(Symbol.Intern("Factory"), constructor.Rest.Head);
        Assert.Equal("Builder", constructor.Rest.Rest.Head);

        Assert.Equal(1d, newExpression.Rest.Rest.Head);
        Assert.Equal(2d, newExpression.Rest.Rest.Rest.Head);
    }

    [Fact]
    public void ParseClassDeclarationProducesConstructorAndMethods()
    {
        var engine = new JsEngine();
        var program = engine.Parse("class Counter { constructor(start) { this.value = start; } increment() { return this.value; } }");

        var classStatement = Assert.IsType<Cons>(program.Rest.Head);
        Assert.Same(JsSymbols.Class, classStatement.Head);
        Assert.Equal(Symbol.Intern("Counter"), classStatement.Rest.Head);

        var constructor = Assert.IsType<Cons>(classStatement.Rest.Rest.Head);
        Assert.Same(JsSymbols.Lambda, constructor.Head);
        Assert.Equal(Symbol.Intern("Counter"), constructor.Rest.Head); // constructor keeps the class name for recursion

        var constructorParameters = Assert.IsType<Cons>(constructor.Rest.Rest.Head);
        Assert.Equal(Symbol.Intern("start"), constructorParameters.Head);

        var constructorBody = Assert.IsType<Cons>(constructor.Rest.Rest.Rest.Head);
        Assert.Same(JsSymbols.Block, constructorBody.Head);

        var methods = Assert.IsType<Cons>(classStatement.Rest.Rest.Rest.Head);
        var methodEntry = Assert.IsType<Cons>(methods.Head);
        Assert.Same(JsSymbols.Method, methodEntry.Head);
        Assert.Equal("increment", methodEntry.Rest.Head);

        var methodLambda = Assert.IsType<Cons>(methodEntry.Rest.Rest.Head);
        Assert.Same(JsSymbols.Lambda, methodLambda.Head);
        Assert.Null(methodLambda.Rest.Head); // class methods stay anonymous like standard method syntax
    }
}
