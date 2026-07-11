using SharpVision.Controls;
using SharpVision.Input;

namespace SharpVision.Tests.Support;

/// <summary>Provides a named container that records default routed-event behavior.</summary>
internal sealed class RecordingControl: Container
{
    private readonly List<string> _order;

    /// <summary>Initializes a validated named recording container.</summary>
    /// <param name="name">The non-empty stable test name.</param>
    /// <param name="order">The non-null shared event-order log.</param>
    internal RecordingControl(string name, List<string> order)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(order);

        Name = name;
        _order = order;
    }

    /// <summary>Gets the stable test name.</summary>
    internal string Name { get; }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        _order.Add($"{Name}-default");
    }
}
