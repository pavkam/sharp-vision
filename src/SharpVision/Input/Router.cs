using System.Buffers;

using SharpVision.Controls;

namespace SharpVision.Input;

/// <summary>Dispatches typed events over stable ancestry and handler snapshots.</summary>
public static class Router
{
    /// <summary>Routes one typed event from a target according to its strategy.</summary>
    /// <typeparam name="TArgs">The exact event-argument type.</typeparam>
    /// <param name="target">The non-null original target.</param>
    /// <param name="routedEvent">The non-null typed identifier.</param>
    /// <param name="eventArgs">The non-null event payload and route state.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// The attached target is accessed off-dispatcher or arguments are already routing.
    /// </exception>
    /// <exception cref="ObjectDisposedException"><paramref name="target"/> is disposed.</exception>
    public static void Route<TArgs>(
        Control target,
        Event<TArgs> routedEvent,
        TArgs eventArgs) where TArgs : RoutedEventArgs
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(routedEvent);
        ArgumentNullException.ThrowIfNull(eventArgs);
        target.VerifyMutable();

        var depth = 0;

        for (var current = (Control?) target; current is not null; current = current.Parent)
        {
            depth++;
        }

        var route = ArrayPool<Control>.Shared.Rent(depth);
        var index = 0;

        for (var current = (Control?) target; current is not null; current = current.Parent)
        {
            route[index++] = current;
        }

        var sequence = Sequence.Current;
        var began = false;

        try
        {
            eventArgs.Begin(target);
            began = true;

            if (routedEvent.Strategy == Strategy.TunnelBubble)
            {
                eventArgs.Phase = Phase.Preview;

                for (index = depth - 1; index >= 0; index--)
                {
                    route[index].InvokeHandlers(routedEvent, eventArgs, sequence);
                }
            }

            eventArgs.Phase = Phase.Bubble;

            var bubbleCount = routedEvent.Strategy == Strategy.Direct ? 1 : depth;

            for (index = 0; index < bubbleCount; index++)
            {
                route[index].InvokeHandlers(routedEvent, eventArgs, sequence);
            }

            if (!eventArgs.Handled)
            {
                target.InvokeDefault(eventArgs);
            }
        }
        finally
        {
            if (began)
            {
                eventArgs.End();
            }

            Array.Clear(route, 0, depth);
            ArrayPool<Control>.Shared.Return(route);
        }
    }
}
