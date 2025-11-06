using System;
using System.Collections.Generic;

namespace Asynkron.JsEngine.Evaluation;

/// <summary>
/// Represents a lexical environment for the interpreter.
/// </summary>
internal sealed class Environment
{
    private readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);
    private readonly Environment? _parent;

    private Environment(Environment? parent)
    {
        _parent = parent;
    }

    public static Environment CreateGlobal()
    {
        var environment = new Environment(null);
        Builtins.Populate(environment);
        return environment;
    }

    public Environment CreateChild() => new(this);

    public void Define(string name, object? value)
    {
        _values[name] = value;
    }

    public void Assign(string name, object? value)
    {
        if (_values.ContainsKey(name))
        {
            _values[name] = value;
            return;
        }

        if (_parent is not null)
        {
            _parent.Assign(name, value);
            return;
        }

        throw new InvalidOperationException($"Undefined symbol '{name}'.");
    }

    public object? Lookup(string name)
    {
        if (_values.TryGetValue(name, out var value))
        {
            return value;
        }

        if (_parent is not null)
        {
            return _parent.Lookup(name);
        }

        throw new InvalidOperationException($"Undefined symbol '{name}'.");
    }
}
