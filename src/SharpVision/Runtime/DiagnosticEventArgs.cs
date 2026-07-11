using SharpVision.Terminal.Protocols;

namespace SharpVision.Runtime;

/// <summary>Provides one redacted terminal protocol diagnostic.</summary>
public sealed class DiagnosticEventArgs: EventArgs
{
    /// <summary>Initializes one redacted diagnostic event.</summary>
    /// <param name="diagnostic">The immutable non-sensitive diagnostic.</param>
    public DiagnosticEventArgs(Diagnostic diagnostic) => Diagnostic = diagnostic;

    /// <summary>Gets the immutable non-sensitive diagnostic.</summary>
    public Diagnostic Diagnostic { get; }
}
