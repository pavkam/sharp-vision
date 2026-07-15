// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;


/// <summary>Stores one typed routed-handler registration.</summary>
/// <typeparam name="TArgs">The exact routed argument type.</typeparam>
internal sealed class Registration<TArgs>: IHandler, IDisposable where TArgs : RoutedEventArgs
{
    /// <summary>Initializes one live handler registration.</summary>
    internal Registration(
        Control owner,
        Event<TArgs> routedEvent,
        EventHandler<TArgs> handler,
        bool handledEventsToo,
        long sequence)
    {
        Owner = owner;
        RoutedEvent = routedEvent;
        Handler = handler;
        HandledEventsToo = handledEventsToo;
        Order = sequence;
    }

    private Control? Owner { get; set; }

    private Event<TArgs>? RoutedEvent { get; set; }

    private EventHandler<TArgs>? Handler { get; set; }

    private bool HandledEventsToo { get; }

    private long Order { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        var owner = Owner;

        if (owner is null)
        {
            return;
        }

        owner.RemoveHandler(this);
    }

    /// <inheritdoc/>
    public bool Matches(IEvent routedEvent, Delegate handler) =>
        ReferenceEquals(RoutedEvent, routedEvent) && Equals(Handler, handler);

    /// <inheritdoc/>
    public void Invoke(
        Control sender,
        IEvent routedEvent,
        RoutedEventArgs eventArgs,
        long sequence)
    {
        var handler = Handler;

        if (handler is null || Order > sequence ||
            !ReferenceEquals(RoutedEvent, routedEvent) ||
            (eventArgs.Handled && !HandledEventsToo))
        {
            return;
        }

        handler(sender, (TArgs) eventArgs);
    }

    /// <inheritdoc/>
    public void Detach()
    {
        Owner = null;
        RoutedEvent = null;
        Handler = null;
    }
}
