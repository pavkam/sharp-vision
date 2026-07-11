using SharpVision.Terminal.Protocols;

namespace SharpVision.Runtime;

/// <summary>Provides one redacted terminal protocol diagnostic.</summary>
public sealed class DiagnosticEventArgs(Diagnostic diagnostic): EventArgs
{
    /// <summary>Gets the immutable non-sensitive diagnostic.</summary>
    public Diagnostic Diagnostic { get; } = diagnostic;
}
