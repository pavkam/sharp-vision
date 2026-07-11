using SharpVision.Terminal.Runtime;

namespace SharpVision.Runtime;

/// <summary>Provides a committed terminal resize after root layout.</summary>
public sealed class ResizeEventArgs(Dimensions dimensions): EventArgs
{
    /// <summary>Gets the committed cell and optional pixel dimensions.</summary>
    public Dimensions Dimensions { get; } = dimensions;
}
