using SharpVision.Terminal.Input;

namespace SharpVision.Input;

/// <summary>Provides an immutable terminal focus transition.</summary>
/// <param name="focus">The decoded focus transition.</param>
public sealed class FocusEventArgs(Focus focus): RoutedEventArgs
{
    /// <summary>Gets the decoded focus transition.</summary>
    public Focus Focus { get; } = focus;
}
