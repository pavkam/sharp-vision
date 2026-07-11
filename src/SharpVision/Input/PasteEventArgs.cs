using SharpVision.Terminal.Input;

namespace SharpVision.Input;

/// <summary>Provides an owned immutable bracketed-paste payload.</summary>
public sealed class PasteEventArgs: RoutedEventArgs
{
    /// <summary>Initializes paste event arguments.</summary>
    /// <param name="paste">The non-null owned paste payload.</param>
    /// <exception cref="ArgumentNullException"><paramref name="paste"/> is null.</exception>
    public PasteEventArgs(Paste paste)
    {
        ArgumentNullException.ThrowIfNull(paste);
        Paste = paste;
    }

    /// <summary>Gets the owned paste payload.</summary>
    public Paste Paste { get; }
}
