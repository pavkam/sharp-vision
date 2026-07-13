// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Support;


/// <summary>Stores one independently modeled terminal cell.</summary>
internal readonly record struct ModelCell
{
    /// <summary>Initializes one validated modeled terminal cell.</summary>
    internal ModelCell(string text, Style style, int width, bool isContinuation, int leadX)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (width is < 0 or > 2)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "A modeled width must be zero through two.");
        }

        if (isContinuation != (width == 0))
        {
            throw new ArgumentException("Only continuations have zero width.", nameof(isContinuation));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(leadX);
        Text = text;
        Style = style;
        Width = width;
        IsContinuation = isContinuation;
        LeadX = leadX;
    }

    /// <summary>Gets the default blank modeled cell.</summary>
    internal static ModelCell Blank { get; } = new(" ", Style.Default, 1, false, 0);

    /// <summary>Gets the complete grapheme text.</summary>
    internal string Text { get; }

    /// <summary>Gets the semantic cell style.</summary>
    internal Style Style { get; init; }

    /// <summary>Gets the grapheme cell width.</summary>
    internal int Width { get; }

    /// <summary>Gets whether the cell continues a wide grapheme.</summary>
    internal bool IsContinuation { get; }

    /// <summary>Gets the owning lead column.</summary>
    internal int LeadX { get; }
}
