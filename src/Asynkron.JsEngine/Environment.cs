namespace Asynkron.JsEngine;

internal sealed class Environment
{
    private readonly Dictionary<Symbol, object?> _values = new();
    private readonly Environment? _enclosing;

    public Environment(Environment? enclosing = null)
    {
        _enclosing = enclosing;
    }

    public void Define(Symbol name, object? value)
    {
        _values[name] = value;
    }

    public object? Get(Symbol name)
    {
        if (_values.TryGetValue(name, out var value))
        {
            return value;
        }

        if (_enclosing is not null)
        {
            return _enclosing.Get(name);
        }

        throw new InvalidOperationException($"Undefined symbol '{name.Name}'.");
    }

    public void Assign(Symbol name, object? value)
    {
        if (_values.ContainsKey(name))
        {
            _values[name] = value;
            return;
        }

        if (_enclosing is not null)
        {
            _enclosing.Assign(name, value);
            return;
        }

        throw new InvalidOperationException($"Undefined symbol '{name.Name}'.");
    }
}
