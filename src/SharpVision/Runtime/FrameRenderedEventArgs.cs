using SharpVision.Terminal.Rendering;

namespace SharpVision.Runtime;

/// <summary>Provides metrics for one flushed and committed terminal frame.</summary>
public sealed class FrameRenderedEventArgs(Metrics metrics): EventArgs
{
    /// <summary>Gets completed renderer metrics.</summary>
    public Metrics Metrics { get; } = metrics;
}
