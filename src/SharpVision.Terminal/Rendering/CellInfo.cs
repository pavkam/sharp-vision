// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Rendering;


/// <summary>Exposes non-owning semantic cell metadata without pooled grapheme memory.</summary>
public readonly record struct CellInfo
{
    /// <summary>Initializes validated semantic cell metadata.</summary>
    /// <param name="style">The semantic cell style.</param>
    /// <param name="width">The lead width, one for blank, or zero for continuation.</param>
    /// <param name="isContinuation">Whether the cell refers to a preceding lead.</param>
    /// <param name="lead">The lead coordinate when this is a continuation.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="width"/> is outside zero through two.</exception>
    /// <exception cref="ArgumentException">The width and continuation state disagree.</exception>
    public CellInfo(CellStyle style, int width, bool isContinuation, Point lead)
    {
        if (width is < 0 or > 2)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "A cell width must be zero, one, or two.");
        }

        if (isContinuation != (width == 0))
        {
            throw new ArgumentException(
                "Only a continuation has zero width, and every continuation has zero width.",
                nameof(isContinuation));
        }

        Style = style;
        Width = width;
        IsContinuation = isContinuation;
        Lead = lead;
    }

    /// <summary>Gets the default blank semantic cell.</summary>
    public static CellInfo Blank { get; } = new(CellStyle.Default, 1, false, default);

    /// <summary>Gets the semantic cell style.</summary>
    public CellStyle Style { get; }

    /// <summary>Gets the lead width, one for blank, or zero for continuation.</summary>
    public int Width { get; }

    /// <summary>Gets whether the cell refers to a preceding lead.</summary>
    public bool IsContinuation { get; }

    /// <summary>Gets the lead coordinate when this is a continuation.</summary>
    public Point Lead { get; }

    /// <summary>Deconstructs the semantic cell metadata.</summary>
    public void Deconstruct(out CellStyle style, out int width, out bool isContinuation, out Point lead)
    {
        style = Style;
        width = Width;
        isContinuation = IsContinuation;
        lead = Lead;
    }
}
