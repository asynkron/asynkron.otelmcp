namespace Asynkron.JsEngine;

/// <summary>
/// Centralised symbol definitions so parser and evaluator agree on structure.
/// </summary>
public static class JsSymbols
{
    public static readonly Symbol Program = Symbol.Intern("program");
    public static readonly Symbol Let = Symbol.Intern("let");
    public static readonly Symbol Function = Symbol.Intern("function");
    public static readonly Symbol Class = Symbol.Intern("class");
    public static readonly Symbol Block = Symbol.Intern("block");
    public static readonly Symbol Return = Symbol.Intern("return");
    public static readonly Symbol ExpressionStatement = Symbol.Intern("expr-stmt");
    public static readonly Symbol Assign = Symbol.Intern("assign");
    public static readonly Symbol Call = Symbol.Intern("call");
    public static readonly Symbol Negate = Symbol.Intern("negate");
    public static readonly Symbol Not = Symbol.Intern("not");
    public static readonly Symbol Lambda = Symbol.Intern("lambda");
    public static readonly Symbol ObjectLiteral = Symbol.Intern("object");
    public static readonly Symbol Property = Symbol.Intern("prop");
    public static readonly Symbol Method = Symbol.Intern("method");
    public static readonly Symbol GetProperty = Symbol.Intern("get-prop");
    public static readonly Symbol SetProperty = Symbol.Intern("set-prop");
    public static readonly Symbol This = Symbol.Intern("this");
    public static readonly Symbol New = Symbol.Intern("new");

    public static Symbol Operator(string op) => Symbol.Intern(op);
}
