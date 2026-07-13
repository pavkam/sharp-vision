namespace SharpVision.Runtime;

using SharpVision.Terminal.Runtime;

/// <summary>Provides a committed terminal resize after root layout.</summary>
public sealed class ResizeEventArgs: EventArgs
{
    /// <summary>Initializes one committed terminal resize.</summary>
    /// <param name="dimensions">The validated cell and optional pixel dimensions.</param>
    public ResizeEventArgs(Dimensions dimensions) => Dimensions = dimensions;

    /// <summary>Gets the committed cell and optional pixel dimensions.</summary>
    public Dimensions Dimensions { get; }
}
