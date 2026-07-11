using SharpVision.Terminal.Input;

namespace SharpVision.Input;

/// <summary>Provides an owned immutable bracketed-paste payload.</summary>
/// <param name="paste">The immutable owned paste payload.</param>
public sealed class PasteEventArgs(Paste paste): RoutedEventArgs
{
    /// <summary>Gets the owned paste payload.</summary>
    public Paste Paste { get; } = paste;
}
