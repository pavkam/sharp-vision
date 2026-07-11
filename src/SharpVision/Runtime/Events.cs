using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Rendering;
using SharpVision.Terminal.Runtime;

namespace SharpVision.Runtime;

/// <summary>Provides a committed terminal resize after root layout.</summary>
public sealed class ResizeEventArgs(Dimensions dimensions): EventArgs
{
    /// <summary>Gets the committed cell and optional pixel dimensions.</summary>
    public Dimensions Dimensions { get; } = dimensions;
}

/// <summary>Provides metrics for one flushed and committed terminal frame.</summary>
public sealed class FrameRenderedEventArgs(Metrics metrics): EventArgs
{
    /// <summary>Gets completed renderer metrics.</summary>
    public Metrics Metrics { get; } = metrics;
}

/// <summary>Provides cancellation for an explicit application stop request.</summary>
public sealed class StoppingEventArgs: EventArgs
{
    /// <summary>Gets or sets whether an explicit stop should be cancelled.</summary>
    public bool Cancel { get; set; }
}

/// <summary>Provides one redacted terminal protocol diagnostic.</summary>
public sealed class DiagnosticEventArgs(Diagnostic diagnostic): EventArgs
{
    /// <summary>Gets the immutable non-sensitive diagnostic.</summary>
    public Diagnostic Diagnostic { get; } = diagnostic;
}
