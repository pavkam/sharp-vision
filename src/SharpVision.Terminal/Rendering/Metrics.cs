// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Rendering;

using Graphics;

/// <summary>Reports work completed by one successfully committed frame render.</summary>
[PublicAPI]
public readonly record struct Metrics
{
    private static readonly IReadOnlyList<GraphicsPlacementDiagnostic> _noGraphicsDiagnostics =
        Array.Empty<GraphicsPlacementDiagnostic>();

    /// <summary>Initializes validated frame-render metrics with no graphics placement diagnostics.</summary>
    /// <param name="bytes">The non-negative transmitted byte count.</param>
    /// <param name="writes">The non-negative transport-write count.</param>
    /// <param name="spans">The non-negative damage-span count.</param>
    /// <param name="full">Whether the operation encoded a full redraw.</param>
    /// <param name="elapsed">The non-negative elapsed time.</param>
    /// <exception cref="ArgumentOutOfRangeException">A count or elapsed time is negative.</exception>
    public Metrics(int bytes, int writes, int spans, bool full, TimeSpan elapsed)
        : this(bytes, writes, spans, full, elapsed, _noGraphicsDiagnostics)
    {
    }

    /// <summary>Initializes validated frame-render metrics.</summary>
    /// <param name="bytes">The non-negative transmitted byte count.</param>
    /// <param name="writes">The non-negative transport-write count.</param>
    /// <param name="spans">The non-negative damage-span count.</param>
    /// <param name="full">Whether the operation encoded a full redraw.</param>
    /// <param name="elapsed">The non-negative elapsed time.</param>
    /// <param name="graphicsDiagnostics">
    /// The non-null graphics placements that fell back to ordinary cells during this frame.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">A count or elapsed time is negative.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="graphicsDiagnostics"/> is null.</exception>
    public Metrics(
        int bytes,
        int writes,
        int spans,
        bool full,
        TimeSpan elapsed,
        IReadOnlyList<GraphicsPlacementDiagnostic> graphicsDiagnostics)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);
        ArgumentOutOfRangeException.ThrowIfNegative(writes);
        ArgumentOutOfRangeException.ThrowIfNegative(spans);
        ArgumentNullException.ThrowIfNull(graphicsDiagnostics);

        if (elapsed < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsed), elapsed, "Elapsed time cannot be negative.");
        }

        Bytes = bytes;
        Writes = writes;
        Spans = spans;
        Full = full;
        Elapsed = elapsed;
        GraphicsDiagnostics = graphicsDiagnostics;
    }

    /// <summary>Gets the number of bytes passed to the transport.</summary>
    public int Bytes { get; }

    /// <summary>Gets the number of transport writes.</summary>
    public int Writes { get; }

    /// <summary>Gets the number of semantic damage spans encoded.</summary>
    public int Spans { get; }

    /// <summary>Gets whether the operation encoded a full redraw.</summary>
    public bool Full { get; }

    /// <summary>Gets the elapsed encode, write, flush, and commit time.</summary>
    public TimeSpan Elapsed { get; }

    /// <summary>
    /// Gets graphics placements that fell back to ordinary cells during this frame. Empty when the
    /// active backend encoded every placement, or when no graphics backend is configured.
    /// </summary>
    public IReadOnlyList<GraphicsPlacementDiagnostic> GraphicsDiagnostics { get; } = _noGraphicsDiagnostics;

    /// <summary>Deconstructs the frame-render metrics.</summary>
    /// <param name="bytes">Receives the transmitted byte count.</param>
    /// <param name="writes">Receives the transport-write count.</param>
    /// <param name="spans">Receives the damage-span count.</param>
    /// <param name="full">Receives whether the operation was a full redraw.</param>
    /// <param name="elapsed">Receives the elapsed time.</param>
    public void Deconstruct(out int bytes, out int writes, out int spans, out bool full, out TimeSpan elapsed)
    {
        bytes = Bytes;
        writes = Writes;
        spans = Spans;
        full = Full;
        elapsed = Elapsed;
    }

    /// <summary>Deconstructs the frame-render metrics, including graphics placement diagnostics.</summary>
    /// <param name="bytes">Receives the transmitted byte count.</param>
    /// <param name="writes">Receives the transport-write count.</param>
    /// <param name="spans">Receives the damage-span count.</param>
    /// <param name="full">Receives whether the operation was a full redraw.</param>
    /// <param name="elapsed">Receives the elapsed time.</param>
    /// <param name="graphicsDiagnostics">Receives the graphics placements that fell back to ordinary cells.</param>
    public void Deconstruct(
        out int bytes,
        out int writes,
        out int spans,
        out bool full,
        out TimeSpan elapsed,
        out IReadOnlyList<GraphicsPlacementDiagnostic> graphicsDiagnostics)
    {
        bytes = Bytes;
        writes = Writes;
        spans = Spans;
        full = Full;
        elapsed = Elapsed;
        graphicsDiagnostics = GraphicsDiagnostics;
    }
}
