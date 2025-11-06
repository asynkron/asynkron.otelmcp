using Asynkron.JsEngine.Evaluation;
using Asynkron.JsEngine.Parsing;
using Asynkron.JsEngine.SExpressions;
using Esprima;
using EnvironmentAlias = Asynkron.JsEngine.Evaluation.Environment;

namespace Asynkron.JsEngine;

/// <summary>
/// Provides parsing and execution capabilities for JavaScript source code using a Lisp-inspired interpreter.
/// </summary>
public sealed class JsExecutionEngine
{
    private readonly ParserOptions _options;

    public JsExecutionEngine(ParserOptions? options = null)
    {
        _options = options ?? new ParserOptions();
    }

    /// <summary>
    /// Parses JavaScript source into an S-expression tree.
    /// </summary>
    public SExpr Parse(string source)
    {
        var parser = new JavaScriptParser(source, _options);
        var script = parser.ParseScript();
        return SExpressionBuilder.BuildProgram(script);
    }

    /// <summary>
    /// Parses and executes JavaScript code by interpreting the resulting S-expressions.
    /// </summary>
    public object? Execute(string source)
    {
        var expression = Parse(source);
        if (expression is Nil)
        {
            return null;
        }

        var environment = EnvironmentAlias.CreateGlobal();
        try
        {
            return Evaluator.Evaluate(expression, environment);
        }
        catch (Evaluator.ReturnSignal signal)
        {
            return signal.Value;
        }
    }
}
