namespace SharpVision.Terminal.Rendering;

/// <summary>Reports one completed in-memory frame encoding operation.</summary>
/// <param name="Spans">The number of damage spans encoded.</param>
/// <param name="Full">Whether the target was encoded as a full redraw.</param>
public readonly record struct EncodeResult(int Spans, bool Full);
