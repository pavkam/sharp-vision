// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Rendering;

using Graphics;

/// <summary>Reports work completed by one successfully committed frame render.</summary>
[PublicAPI]
public readonly record struct RenderMetrics
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
    public RenderMetrics(int bytes, int writes, int spans, bool full, TimeSpan elapsed)
        : this(bytes, writes, spans, full, elapsed, usedFallback: false, _noGraphicsDiagnostics)
    {
    }

    /// <summary>Initializes validated frame-render metrics.</summary>
    /// <param name="bytes">The non-negative transmitted byte count.</param>
    /// <param name="writes">The non-negative transport-write count.</param>
    /// <param name="spans">The non-negative damage-span count.</param>
    /// <param name="full">Whether the operation encoded a full redraw.</param>
    /// <param name="elapsed">The non-negative elapsed time.</param>
    /// <param name="graphicsDiagnostics">
    /// The non-null graphics placements that fell back to ordinary cells during this frame. The
    /// constructor copies nonempty values before returning.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">A count or elapsed time is negative.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="graphicsDiagnostics"/> is null.</exception>
    public RenderMetrics(
        int bytes,
        int writes,
        int spans,
        bool full,
        TimeSpan elapsed,
        IReadOnlyList<GraphicsPlacementDiagnostic> graphicsDiagnostics) : this(
        bytes,
        writes,
        spans,
        full,
        elapsed,
        graphicsDiagnostics is { Count: > 0 },
        graphicsDiagnostics)
    {
    }

    /// <summary>Initializes validated frame-render metrics including fidelity fallback.</summary>
    /// <param name="bytes">The non-negative transmitted byte count.</param>
    /// <param name="writes">The non-negative transport-write count.</param>
    /// <param name="spans">The non-negative damage-span count.</param>
    /// <param name="full">Whether the operation encoded a full redraw.</param>
    /// <param name="elapsed">The non-negative elapsed time.</param>
    /// <param name="usedFallback">Whether this frame used a lower-fidelity presentation path.</param>
    /// <param name="graphicsDiagnostics">The non-null graphics placements that fell back to cells.</param>
    /// <exception cref="ArgumentOutOfRangeException">A count or elapsed time is negative.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="graphicsDiagnostics"/> is null.</exception>
    public RenderMetrics(
        int bytes,
        int writes,
        int spans,
        bool full,
        TimeSpan elapsed,
        bool usedFallback,
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
        UsedFallback = usedFallback;
        GraphicsDiagnosticsSnapshot = graphicsDiagnostics.Count == 0
            ? _noGraphicsDiagnostics
            : Array.AsReadOnly(graphicsDiagnostics.ToArray());
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

    /// <summary>Gets whether this frame used a lower-fidelity presentation path.</summary>
    public bool UsedFallback { get; }

    /// <summary>
    /// Gets the immutable snapshot of graphics placements that fell back to ordinary cells during
    /// this frame. Empty when the active backend encoded every placement, or when no graphics
    /// backend is configured.
    /// </summary>
    public IReadOnlyList<GraphicsPlacementDiagnostic> GraphicsDiagnostics =>
        GraphicsDiagnosticsSnapshot ?? _noGraphicsDiagnostics;

    private IReadOnlyList<GraphicsPlacementDiagnostic>? GraphicsDiagnosticsSnapshot { get; }

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

    /// <summary>Deconstructs frame metrics including fidelity fallback and graphics diagnostics.</summary>
    /// <param name="bytes">Receives the transmitted byte count.</param>
    /// <param name="writes">Receives the transport-write count.</param>
    /// <param name="spans">Receives the damage-span count.</param>
    /// <param name="full">Receives whether the operation was a full redraw.</param>
    /// <param name="elapsed">Receives the elapsed time.</param>
    /// <param name="usedFallback">Receives whether the frame used lower-fidelity presentation.</param>
    /// <param name="graphicsDiagnostics">Receives graphics placements that fell back to cells.</param>
    public void Deconstruct(
        out int bytes,
        out int writes,
        out int spans,
        out bool full,
        out TimeSpan elapsed,
        out bool usedFallback,
        out IReadOnlyList<GraphicsPlacementDiagnostic> graphicsDiagnostics)
    {
        bytes = Bytes;
        writes = Writes;
        spans = Spans;
        full = Full;
        elapsed = Elapsed;
        usedFallback = UsedFallback;
        graphicsDiagnostics = GraphicsDiagnostics;
    }
}
