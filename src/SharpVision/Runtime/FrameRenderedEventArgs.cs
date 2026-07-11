using SharpVision.Terminal.Rendering;

namespace SharpVision.Runtime;

/// <summary>Provides metrics for one flushed and committed terminal frame.</summary>
public sealed class FrameRenderedEventArgs: EventArgs
{
    /// <summary>Initializes one completed frame event.</summary>
    /// <param name="metrics">The validated completed renderer metrics.</param>
    public FrameRenderedEventArgs(Metrics metrics) => Metrics = metrics;

    /// <summary>Gets completed renderer metrics.</summary>
    public Metrics Metrics { get; }
}
