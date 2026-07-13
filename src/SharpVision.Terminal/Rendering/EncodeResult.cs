// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Rendering;

/// <summary>Reports one completed in-memory frame encoding operation.</summary>
public readonly record struct EncodeResult
{
    /// <summary>Initializes validated frame-encoding metrics.</summary>
    /// <param name="spans">The number of damage spans encoded.</param>
    /// <param name="full">Whether the target was encoded as a full redraw.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="spans"/> is negative.</exception>
    public EncodeResult(int spans, bool full)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(spans);

        Spans = spans;
        Full = full;
    }

    /// <summary>Gets the number of damage spans encoded.</summary>
    public int Spans { get; }

    /// <summary>Gets whether the target was encoded as a full redraw.</summary>
    public bool Full { get; }

    /// <summary>Deconstructs the encoding metrics.</summary>
    public void Deconstruct(out int spans, out bool full)
    {
        spans = Spans;
        full = Full;
    }
}
