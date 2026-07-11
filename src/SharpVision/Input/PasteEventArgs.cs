using SharpVision.Terminal.Input;

namespace SharpVision.Input;

/// <summary>Provides an owned immutable bracketed-paste payload.</summary>
public sealed class PasteEventArgs: RoutedEventArgs
{
    /// <summary>Initializes routed bracketed-paste input.</summary>
    /// <param name="paste">The immutable owned paste payload.</param>
    public PasteEventArgs(Paste paste) => Paste = paste;

    /// <summary>Gets the owned paste payload.</summary>
    public Paste Paste { get; }
}
