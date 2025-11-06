using System;
using System.Collections.Generic;
using Asynkron.JsEngine.SExpressions;

namespace Asynkron.JsEngine.Evaluation;

/// <summary>
/// Represents a user-defined function closure.
/// </summary>
internal sealed class LambdaValue
{
    private readonly IReadOnlyList<string> _parameters;
    private readonly SExpr _body;
    private readonly Environment _closure;

    public LambdaValue(IReadOnlyList<string> parameters, SExpr body, Environment closure)
    {
        _parameters = parameters;
        _body = body;
        _closure = closure;
    }

    public object? Invoke(IReadOnlyList<object?> arguments)
    {
        if (arguments.Count != _parameters.Count)
        {
            throw new InvalidOperationException($"Expected {_parameters.Count} argument(s) but received {arguments.Count}.");
        }

        var frame = _closure.CreateChild();
        for (var index = 0; index < _parameters.Count; index++)
        {
            frame.Define(_parameters[index], arguments[index]);
        }

        try
        {
            return Evaluator.Evaluate(_body, frame);
        }
        catch (Evaluator.ReturnSignal signal)
        {
            return signal.Value;
        }
    }
}
