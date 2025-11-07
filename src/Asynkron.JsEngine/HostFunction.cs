namespace Asynkron.JsEngine;

internal sealed class HostFunction : IJsCallable
{
    private readonly Func<object?, IReadOnlyList<object?>, object?> _handler;

    public HostFunction(Func<IReadOnlyList<object?>, object?> handler)
    {
        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        _handler = (_, args) => handler(args);
    }

    public HostFunction(Func<object?, IReadOnlyList<object?>, object?> handler)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public object? Invoke(IReadOnlyList<object?> arguments, object? thisValue) => _handler(thisValue, arguments);
}
