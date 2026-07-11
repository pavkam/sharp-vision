using SharpVision.Terminal.Input;

namespace SharpVision.Input;

/// <summary>Provides an immutable decoded keyboard transition.</summary>
public sealed class KeyEventArgs: RoutedEventArgs
{
    /// <summary>Initializes routed keyboard input.</summary>
    /// <param name="stroke">The decoded keyboard transition.</param>
    public KeyEventArgs(Stroke stroke) => Stroke = stroke;

    /// <summary>Gets the decoded keyboard transition.</summary>
    public Stroke Stroke { get; }
}
