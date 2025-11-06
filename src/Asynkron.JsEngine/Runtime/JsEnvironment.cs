using System;
using System.Collections.Generic;

namespace Asynkron.JsEngine.Runtime;

/// <summary>
/// Represents a lexical scope for the Lisp-like JavaScript runtime.
/// Tracks bindings with mutability information so <c>const</c> declarations
/// cannot be reassigned.
/// </summary>
public sealed class JsEnvironment
{
    private readonly Dictionary<string, Binding> _bindings;
    private readonly JsEnvironment? _parent;

    private JsEnvironment(Dictionary<string, Binding> bindings, JsEnvironment? parent)
    {
        _bindings = bindings;
        _parent = parent;
    }

    public JsEnvironment(JsEnvironment? parent = null)
    {
        _bindings = new Dictionary<string, Binding>(StringComparer.Ordinal);
        _parent = parent;
    }

    public JsEnvironment CreateChild() => new(new Dictionary<string, Binding>(StringComparer.Ordinal), this);

    public void Define(string name, object? value, bool isMutable = true)
    {
        _bindings[name] = new Binding(value, isMutable);
    }

    public void Set(string name, object? value)
    {
        if (_bindings.TryGetValue(name, out var binding))
        {
            if (!binding.IsMutable)
            {
                throw new InvalidOperationException($"Cannot assign to constant '{name}'.");
            }

            binding.Value = value;
            return;
        }

        if (_parent is not null)
        {
            _parent.Set(name, value);
            return;
        }

        throw new InvalidOperationException($"Undefined symbol '{name}'.");
    }

    public object? Get(string name)
    {
        if (_bindings.TryGetValue(name, out var binding))
        {
            return binding.Value;
        }

        if (_parent is not null)
        {
            return _parent.Get(name);
        }

        throw new InvalidOperationException($"Undefined symbol '{name}'.");
    }

    private sealed class Binding
    {
        public Binding(object? value, bool isMutable)
        {
            Value = value;
            IsMutable = isMutable;
        }

        public object? Value { get; set; }

        public bool IsMutable { get; }
    }
}
