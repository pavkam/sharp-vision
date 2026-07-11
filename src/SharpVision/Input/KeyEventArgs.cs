using SharpVision.Terminal.Input;

namespace SharpVision.Input;

/// <summary>Provides an immutable decoded keyboard transition.</summary>
/// <param name="stroke">The decoded keyboard transition.</param>
public sealed class KeyEventArgs(Stroke stroke): RoutedEventArgs
{
    /// <summary>Gets the decoded keyboard transition.</summary>
    public Stroke Stroke { get; } = stroke;
}
