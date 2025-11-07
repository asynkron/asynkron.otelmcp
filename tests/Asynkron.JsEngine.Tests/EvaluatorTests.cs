using Asynkron.JsEngine;

namespace Asynkron.JsEngine.Tests;

public class EvaluatorTests
{
    [Fact]
    public void EvaluateArithmeticAndVariableLookup()
    {
        var engine = new JsEngine();
        var result = engine.Evaluate("let answer = 1 + 2 * 3; answer;");
        Assert.Equal(7d, result);
    }

    [Fact]
    public void EvaluateFunctionDeclarationAndInvocation()
    {
        var engine = new JsEngine();
        var source = "function add(a, b) { return a + b; } let result = add(2, 3); result;";
        var result = engine.Evaluate(source);
        Assert.Equal(5d, result);
    }

    [Fact]
    public void EvaluateClosureCapturesOuterVariable()
    {
        var engine = new JsEngine();
        var source = "function makeAdder(x) { function inner(y) { return x + y; } return inner; } let plusTen = makeAdder(10); let fifteen = plusTen(5); fifteen;";
        var result = engine.Evaluate(source);
        Assert.Equal(15d, result);
    }

    [Fact]
    public void EvaluateFunctionExpression()
    {
        var engine = new JsEngine();
        var source = "let add = function(a, b) { return a + b; }; add(4, 5);";
        var result = engine.Evaluate(source);
        Assert.Equal(9d, result);
    }

    [Fact]
    public void HostFunctionInterop()
    {
        var captured = new List<object?>();
        var engine = new JsEngine();
        engine.SetGlobalFunction("collect", args =>
        {
            captured.AddRange(args);
            return args.Count;
        });

        var result = engine.Evaluate("collect(\"hello\", 3); collect(\"world\");");

        Assert.Equal(1, result); // last call returns number of args
        Assert.Collection(captured,
            item => Assert.Equal("hello", item),
            item => Assert.Equal(3d, item),
            item => Assert.Equal("world", item));
    }

    [Fact]
    public void EvaluateObjectLiteralAndPropertyUsage()
    {
        var engine = new JsEngine();
        var source = "let obj = { a: 10, x: function () { return 5; } }; let total = obj.a + obj.x(); total;";

        var result = engine.Evaluate(source);

        Assert.Equal(15d, result); // object property read plus function invocation
    }

    [Fact]
    public void MethodInvocationBindsThis()
    {
        var engine = new JsEngine();
        var source = "let obj = { x: 10, f: function () { return this.x; } }; obj.f();";

        var result = engine.Evaluate(source);

        Assert.Equal(10d, result);
    }

    [Fact]
    public void HostFunctionReceivesThisBinding()
    {
        var engine = new JsEngine();
        engine.SetGlobalFunction("reflectThis", (self, _) => self);

        var result = engine.Evaluate("let obj = { value: 42, reflect: reflectThis }; obj.reflect();");

        var thisBinding = Assert.IsAssignableFrom<IDictionary<string, object?>>(result);
        Assert.Equal(42d, thisBinding["value"]);
    }

    [Fact]
    public void PrototypeLookupResolvesInheritedMethods()
    {
        var engine = new JsEngine();
        var source = @"
let base = {
    multiplier: 2,
    calculate: function(value) { return value * this.multiplier; }
};
let derived = { value: 7, __proto__: base };
derived.calculate(derived.value);
";

        var result = engine.Evaluate(source);

        Assert.Equal(14d, result);
    }

    [Fact]
    public void PrototypeAssignmentLinksObjectsAfterCreation()
    {
        var engine = new JsEngine();
        var source = @"
let base = { greet: function() { return ""hi "" + this.name; } };
let user = { name: ""Alice"" };
user.__proto__ = base;
user.greet();
";

        var result = engine.Evaluate(source);

        Assert.Equal("hi Alice", result);
    }

    [Fact]
    public void NewCreatesInstancesWithConstructorPrototypes()
    {
        var engine = new JsEngine();
        var source = @"
function Person(name) {
    this.name = name;
}
Person.prototype.describe = function() { return ""Person:"" + this.name; };
let person = new Person(""Bob"");
person.describe();
";

        var result = engine.Evaluate(source);

        Assert.Equal("Person:Bob", result);
    }

    [Fact]
    public void MethodClosuresCanReachThisViaCapturedReference()
    {
        var engine = new JsEngine();
        var source = @"
let obj = {
    value: 10,
    makeIncrementer: function(step) {
        let receiver = this; // capture the current method receiver for later use
        return function(extra) {
            return receiver.value + step + extra;
        };
    }
};
let inc = obj.makeIncrementer(5);
inc(3);
";

        var result = engine.Evaluate(source);

        Assert.Equal(18d, result);
    }

    [Fact]
    public void DistinctMethodCallsProvideIndependentThisBindings()
    {
        var engine = new JsEngine();
        var source = @"
let factory = {
    create: function(number) {
        return {
            value: number,
            read: function() { return this.value; } // rely on this binding when invoked as a method
        };
    }
};
let first = factory.create(7);
let second = factory.create(8);
first.read() + second.read();
";

        var result = engine.Evaluate(source);

        Assert.Equal(15d, result);
    }
}
