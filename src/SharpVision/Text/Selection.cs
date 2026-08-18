// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Text;

/// <summary>Stores directional grapheme-boundary selection endpoints in UTF-16 units.</summary>
[PublicAPI]
public readonly record struct Selection
{
    /// <summary>Initializes non-negative anchor and active-caret endpoints.</summary>
    /// <param name="anchor">The fixed UTF-16 endpoint used while extending.</param>
    /// <param name="caret">The active UTF-16 endpoint moved by navigation.</param>
    /// <exception cref="ArgumentOutOfRangeException">An endpoint is negative.</exception>
    public Selection(int anchor, int caret)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(anchor);
        ArgumentOutOfRangeException.ThrowIfNegative(caret);
        Anchor = anchor;
        Caret = caret;
    }

    /// <summary>Gets the fixed endpoint used while extending the selection.</summary>
    public int Anchor { get; }

    /// <summary>Gets the active endpoint used for insertion and navigation.</summary>
    public int Caret { get; }

    /// <summary>Gets the normalized inclusive start endpoint.</summary>
    public int Start => Math.Min(Anchor, Caret);

    /// <summary>Gets the normalized UTF-16 length.</summary>
    public int Length => Math.Abs(Anchor - Caret);

    /// <summary>Gets the normalized exclusive end endpoint.</summary>
    public int End => Math.Max(Anchor, Caret);

    /// <summary>Gets whether anchor and caret are the same endpoint.</summary>
    public bool IsEmpty => Anchor == Caret;
}
