using System.Diagnostics.CodeAnalysis;

using SharpVision.Controls;

namespace SharpVision.Input;

/// <summary>Defines how a typed event traverses the control ancestry.</summary>
public enum Strategy
{
    /// <summary>Invokes preview root-to-target, then bubble target-to-root.</summary>
    TunnelBubble,

    /// <summary>Invokes only bubble target-to-root.</summary>
    Bubble,

    /// <summary>Invokes only the target.</summary>
    Direct,
}

/// <summary>Identifies one immutable typed routed event.</summary>
/// <typeparam name="TArgs">The exact event-argument type accepted by handlers.</typeparam>
[SuppressMessage(
    "Naming",
    "CA1716:Identifiers should not match keywords",
    Justification = "Event is the intentional public routed-input domain term.")]
public sealed class Event<TArgs>: IEvent where TArgs : RoutedEventArgs
{
    /// <summary>Initializes a named routed-event identifier.</summary>
    /// <param name="name">The non-empty diagnostic name.</param>
    /// <param name="strategy">The route traversal strategy.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="strategy"/> is unknown.
    /// </exception>
    public Event(string name, Strategy strategy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!Enum.IsDefined(strategy))
        {
            throw new ArgumentOutOfRangeException(
                nameof(strategy),
                strategy,
                "The routing strategy is unknown.");
        }

        Name = name;
        Strategy = strategy;
    }

    /// <summary>Gets the diagnostic event name.</summary>
    public string Name { get; }

    /// <summary>Gets the ancestry traversal strategy.</summary>
    public Strategy Strategy { get; }

    /// <inheritdoc/>
    public override string ToString() => Name;
}

internal interface IEvent
{
    /// <summary>Gets the ancestry traversal strategy.</summary>
    public Strategy Strategy { get; }
}

internal interface IHandler
{
    /// <summary>Gets whether this registration matches an event and delegate identity.</summary>
    public bool Matches(IEvent routedEvent, Delegate handler);

    /// <summary>Invokes the registration when it belongs to the route snapshot.</summary>
    public void Invoke(
        Control sender,
        IEvent routedEvent,
        RoutedEventArgs eventArgs,
        long sequence);

    /// <summary>Breaks every owner and delegate reference without dispatcher validation.</summary>
    public void Detach();
}

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

internal static class Sequence
{
    private static long _value;

    /// <summary>Gets a unique global order for a newly added registration.</summary>
    internal static long Next() => Interlocked.Increment(ref _value);

    /// <summary>Gets the latest order included in a new route snapshot.</summary>
    internal static long Current => Volatile.Read(ref _value);
}
