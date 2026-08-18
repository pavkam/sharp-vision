// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Threading;

/// <summary>Routes async continuations back to the owning <see cref="Dispatcher"/> thread.</summary>
/// <remarks>
/// <see cref="Post"/> silently drops work when the dispatcher is stopping or its queue is full,
/// matching the WPF convention that <see cref="SynchronizationContext.Post"/> must not throw.
/// </remarks>
internal sealed class DispatcherSynchronizationContext: SynchronizationContext
{
    private readonly Dispatcher _dispatcher;

    public DispatcherSynchronizationContext(Dispatcher dispatcher) => _dispatcher = dispatcher;

    /// <inheritdoc/>
    public override void Post(SendOrPostCallback d, object? state)
    {
        ArgumentNullException.ThrowIfNull(d);

        try
        {
            _dispatcher.Post(() => d(state));
        }
        catch (Exception exception) when (exception is ObjectDisposedException or InvalidOperationException)
        {
        }
    }

    /// <inheritdoc/>
    public override void Send(SendOrPostCallback d, object? state)
    {
        ArgumentNullException.ThrowIfNull(d);

        if (_dispatcher.CheckAccess())
        {
            d(state);
            return;
        }

        _dispatcher.InvokeAsync(() => d(state)).AsTask().GetAwaiter().GetResult();
    }

    /// <inheritdoc/>
    public override SynchronizationContext CreateCopy() => new DispatcherSynchronizationContext(_dispatcher);
}
