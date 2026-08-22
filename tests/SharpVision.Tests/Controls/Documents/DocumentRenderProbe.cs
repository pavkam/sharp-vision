// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Documents;

/// <summary>Lays out and paints one detached <see cref="Document"/> into a real frame and exposes its
/// committed cells.</summary>
/// <remarks>
/// A document's projection is a pure function of its tree, the content width, the ambiguous-width
/// policy, and the glyph family, so a detached layout plus one paint is the cheapest honest evidence
/// for spacing, wrapping, markers, bars, rules, and glyph repair. Mounted surfaces stay reserved for
/// input, focus, and live restyling, which a frame alone cannot prove.
/// </remarks>
internal sealed class DocumentRenderProbe: IDisposable
{
    private readonly Frame _frame;
    private readonly Size _size;

    /// <summary>Lays the document out against a fixed viewport and paints one frame.</summary>
    /// <param name="document">The non-null detached document to project.</param>
    /// <param name="size">The positive viewport size in cells.</param>
    /// <param name="ambiguousWidth">The East Asian Ambiguous cell policy the tree and frame share.</param>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A viewport dimension is not positive.</exception>
    internal DocumentRenderProbe(Document document, Size size, Ambiguous ambiguousWidth = Ambiguous.Narrow)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size.Height);

        document.SetCellPolicy(new UnicodePolicy(ambiguousWidth));
        new LayoutEngine().Layout(document, size);
        _size = size;
        _frame = new Frame(size, ambiguousWidth: ambiguousWidth);
        document.Render(_frame.Canvas);
    }

    /// <summary>Gets the exact grapheme committed at one cell, or a single space when the cell is
    /// blank, so a row reads as plain text.</summary>
    /// <param name="x">The zero-based column.</param>
    /// <param name="y">The zero-based row.</param>
    /// <returns>The committed grapheme, or " " for a blank cell.</returns>
    internal string Text(int x, int y)
    {
        var grapheme = FrameOracle.Get(_frame, new Point(x, y));
        return grapheme.Length == 0 ? " " : grapheme;
    }

    /// <summary>Gets one whole committed row as text, with blank cells rendered as spaces and
    /// trailing blanks trimmed.</summary>
    /// <param name="y">The zero-based row.</param>
    /// <returns>The row's committed text.</returns>
    internal string Row(int y)
    {
        var value = new StringBuilder();

        for (var x = 0; x < _size.Width; x++)
        {
            _ = value.Append(Text(x, y));
        }

        return value.ToString().TrimEnd();
    }

    /// <summary>Gets every committed row as text, with trailing blank rows preserved.</summary>
    /// <returns>The committed rows, top to bottom.</returns>
    internal string[] Rows()
    {
        var rows = new string[_size.Height];

        for (var y = 0; y < _size.Height; y++)
        {
            rows[y] = Row(y);
        }

        return rows;
    }

    /// <summary>Gets the complete committed cell, including its resolved terminal style.</summary>
    /// <param name="x">The zero-based column.</param>
    /// <param name="y">The zero-based row.</param>
    /// <returns>The committed cell.</returns>
    internal CellInfo Cell(int x, int y) => _frame.GetCell(new Point(x, y));

    /// <inheritdoc/>
    public void Dispose() => _frame.Dispose();
}
