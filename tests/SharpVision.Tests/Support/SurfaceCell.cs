// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Stores one independently modeled terminal surface cell.</summary>
internal readonly record struct SurfaceCell
{
    /// <summary>Initializes one validated terminal surface cell.</summary>
    /// <param name="text">The non-null complete grapheme or one blank cell.</param>
    /// <param name="style">The resolved terminal cell style.</param>
    /// <param name="width">The grapheme width from zero through two cells.</param>
    /// <param name="isContinuation">Whether this cell continues a width-two grapheme.</param>
    /// <param name="leadX">The zero-based owning lead column.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="width"/> or <paramref name="leadX"/> is outside its valid range.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="isContinuation"/> disagrees with <paramref name="width"/>.
    /// </exception>
    internal SurfaceCell(string text, TerminalStyle style, int width, bool isContinuation, int leadX)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (width is < 0 or > 2)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width,
                "A surface cell width must be zero through two.");
        }

        if (isContinuation != (width == 0))
        {
            throw new ArgumentException("Only continuation cells have zero width.", nameof(isContinuation));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(leadX);
        Text = text;
        Style = style;
        Width = width;
        IsContinuation = isContinuation;
        LeadX = leadX;
    }

    /// <summary>Gets the default blank terminal cell.</summary>
    internal static SurfaceCell Blank { get; } = new(" ", TerminalStyle.Default, 1, false, 0);

    /// <summary>Gets the complete grapheme text, or one space for a blank cell.</summary>
    internal string Text { get; init; }

    /// <summary>Gets the resolved terminal cell style.</summary>
    internal TerminalStyle Style { get; init; }

    /// <summary>Gets the grapheme width in terminal cells.</summary>
    internal int Width { get; }

    /// <summary>Gets whether this cell continues the preceding width-two grapheme.</summary>
    internal bool IsContinuation { get; }

    /// <summary>Gets the zero-based owning lead column.</summary>
    internal int LeadX { get; }
}
