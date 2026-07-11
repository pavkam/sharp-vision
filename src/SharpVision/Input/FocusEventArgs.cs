using SharpVision.Terminal.Input;

namespace SharpVision.Input;

/// <summary>Provides an immutable terminal focus transition.</summary>
public sealed class FocusEventArgs: RoutedEventArgs
{
    /// <summary>Initializes routed terminal focus input.</summary>
    /// <param name="focus">The decoded focus transition.</param>
    public FocusEventArgs(Focus focus) => Focus = focus;

    /// <summary>Gets the decoded focus transition.</summary>
    public Focus Focus { get; }
}
