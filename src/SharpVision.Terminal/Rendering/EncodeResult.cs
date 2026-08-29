// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Rendering;

/// <summary>Reports one completed in-memory frame encoding operation.</summary>
[PublicAPI]
public readonly record struct EncodeResult
{
    /// <summary>Initializes validated frame-encoding metrics.</summary>
    /// <param name="spans">The number of damage spans encoded.</param>
    /// <param name="full">Whether the target was encoded as a full redraw.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="spans"/> is negative.</exception>
    public EncodeResult(int spans, bool full) : this(spans, full, usedFallback: false)
    {
    }

    /// <summary>Initializes validated frame-encoding metrics including fidelity fallback.</summary>
    /// <param name="spans">The number of damage spans encoded.</param>
    /// <param name="full">Whether the target was encoded as a full redraw.</param>
    /// <param name="usedFallback">Whether semantic presentation was projected to lower fidelity.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="spans"/> is negative.</exception>
    public EncodeResult(int spans, bool full, bool usedFallback)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(spans);

        Spans = spans;
        Full = full;
        UsedFallback = usedFallback;
    }

    /// <summary>Gets the number of damage spans encoded.</summary>
    public int Spans { get; }

    /// <summary>Gets whether the target was encoded as a full redraw.</summary>
    public bool Full { get; }

    /// <summary>Gets whether semantic presentation was projected to lower fidelity.</summary>
    public bool UsedFallback { get; }

    /// <summary>Deconstructs the encoding metrics.</summary>
    public void Deconstruct(out int spans, out bool full)
    {
        spans = Spans;
        full = Full;
    }

    /// <summary>Deconstructs encoding metrics including fidelity fallback.</summary>
    /// <param name="spans">Receives the damage-span count.</param>
    /// <param name="full">Receives whether the target was a full redraw.</param>
    /// <param name="usedFallback">Receives whether semantic presentation used lower fidelity.</param>
    public void Deconstruct(out int spans, out bool full, out bool usedFallback)
    {
        spans = Spans;
        full = Full;
        usedFallback = UsedFallback;
    }
}
