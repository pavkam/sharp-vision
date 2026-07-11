using SharpVision.Controls;
using SharpVision.Input;

namespace SharpVision.Tests.Support;

/// <summary>Provides a named container that records default routed-event behavior.</summary>
internal sealed class RecordingControl(string name, List<string> order): Container
{
    /// <summary>Gets the stable test name.</summary>
    internal string Name { get; } = name;

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        order.Add($"{Name}-default");
    }
}
