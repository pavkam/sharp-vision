namespace SharpVision.Terminal.Rendering;

/// <summary>
/// Reports work completed by one successfully committed frame render.
/// </summary>
/// <param name="Bytes">The number of bytes passed to the transport.</param>
/// <param name="Writes">The number of transport writes.</param>
/// <param name="Spans">The number of semantic damage spans encoded.</param>
/// <param name="Full">Whether the operation encoded a full redraw.</param>
/// <param name="Elapsed">The elapsed encode, write, flush, and commit time.</param>
public readonly record struct Metrics(
    int Bytes,
    int Writes,
    int Spans,
    bool Full,
    TimeSpan Elapsed);
