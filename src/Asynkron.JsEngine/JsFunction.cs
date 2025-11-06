namespace Asynkron.JsEngine;

internal sealed class JsFunction : IJsCallable
{
    private readonly Symbol? _name;
    private readonly IReadOnlyList<Symbol> _parameters;
    private readonly Cons _body;
    private readonly Environment _closure;

    public JsFunction(Symbol? name, IReadOnlyList<Symbol> parameters, Cons body, Environment closure)
    {
        _name = name;
        _parameters = parameters;
        _body = body;
        _closure = closure;
    }

    public object? Invoke(IReadOnlyList<object?> arguments)
    {
        if (arguments.Count != _parameters.Count)
        {
            throw new InvalidOperationException($"Function expected {_parameters.Count} arguments but received {arguments.Count}.");
        }

        var environment = new Environment(_closure);
        for (var i = 0; i < _parameters.Count; i++)
        {
            environment.Define(_parameters[i], arguments[i]);
        }

        if (_name is not null)
        {
            environment.Define(_name, this);
        }

        try
        {
            return Evaluator.EvaluateBlock(_body, environment);
        }
        catch (ReturnSignal signal)
        {
            return signal.Value;
        }
    }
}
