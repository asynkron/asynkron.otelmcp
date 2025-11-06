namespace Asynkron.JsEngine;

internal sealed class HostFunction : IJsCallable
{
    private readonly Func<IReadOnlyList<object?>, object?> _handler;

    public HostFunction(Func<IReadOnlyList<object?>, object?> handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public object? Invoke(IReadOnlyList<object?> arguments) => _handler(arguments);
}
