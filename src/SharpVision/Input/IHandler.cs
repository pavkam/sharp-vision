namespace SharpVision.Input;

using SharpVision.Controls;

/// <summary>Provides a non-generic routed-handler registration contract.</summary>
internal interface IHandler
{
    /// <summary>Gets whether this registration matches an event and delegate identity.</summary>
    public bool Matches(IEvent routedEvent, Delegate handler);

    /// <summary>Invokes this registration for one stable route snapshot.</summary>
    public void Invoke(
        Control sender,
        IEvent routedEvent,
        RoutedEventArgs eventArgs,
        long sequence);

    /// <summary>Breaks every owner and delegate reference without dispatcher validation.</summary>
    public void Detach();
}
