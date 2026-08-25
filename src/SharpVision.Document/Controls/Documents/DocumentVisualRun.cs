// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Documents;

/// <summary>Represents one positioned, styled span of cells on a laid-out document line.</summary>
internal readonly struct DocumentVisualRun
{
    private DocumentVisualRun(
        DocumentRunKind kind,
        int column,
        int cells,
        int parsedRunIndex,
        int offset,
        int length,
        Rune glyph,
        ControlBase? control,
        DocumentFaceKind face,
        DocumentFaceKind? foregroundOverride,
        int linkIndex,
        Selection semanticRange)
    {
        Kind = kind;
        Column = column;
        Cells = cells;
        ParsedRunIndex = parsedRunIndex;
        Offset = offset;
        Length = length;
        Glyph = glyph;
        Control = control;
        Face = face;
        ForegroundOverride = foregroundOverride;
        LinkIndex = linkIndex;
        SemanticRange = semanticRange;
    }

    /// <summary>Gets how this run produces cells.</summary>
    public DocumentRunKind Kind { get; }

    /// <summary>Gets the zero-based cell column at which this run starts, relative to the document's
    /// content origin.</summary>
    public int Column { get; }

    /// <summary>Gets the non-negative cell width this run occupies.</summary>
    public int Cells { get; }

    /// <summary>Gets the parsed-run index supplying the display text, or -1 for a
    /// <see cref="DocumentRunKind.Repeat"/> run.</summary>
    public int ParsedRunIndex { get; }

    /// <summary>Gets the UTF-16 offset into the parsed run's display text.</summary>
    public int Offset { get; }

    /// <summary>Gets the UTF-16 length within the parsed run's display text.</summary>
    public int Length { get; }

    /// <summary>Gets the repeated glyph for a <see cref="DocumentRunKind.Repeat"/> run.</summary>
    public Rune Glyph { get; }

    /// <summary>Gets the retained control for a <see cref="DocumentRunKind.Control"/> run, or null.</summary>
    public ControlBase? Control { get; }

    /// <summary>Gets which style face paints this run.</summary>
    public DocumentFaceKind Face { get; }

    /// <summary>Gets the enclosing semantic face whose foreground replaces this run's own
    /// foreground, or <see langword="null"/> when the run uses its ordinary face.</summary>
    public DocumentFaceKind? ForegroundOverride { get; }

    /// <summary>Gets the owning link's index, or -1 when this run is not part of a link.</summary>
    public int LinkIndex { get; }

    /// <summary>Gets the semantic range painted by a generated repeat, or an empty range.</summary>
    public Selection SemanticRange { get; }

    /// <summary>Creates a run painting a slice of parsed display text.</summary>
    /// <param name="column">The zero-based start column.</param>
    /// <param name="cells">The non-negative cell width.</param>
    /// <param name="parsedRunIndex">The parsed-run index supplying the display text.</param>
    /// <param name="offset">The UTF-16 offset into the display text.</param>
    /// <param name="length">The UTF-16 length within the display text.</param>
    /// <param name="face">The painting face.</param>
    /// <param name="linkIndex">The owning link's index, or -1.</param>
    /// <param name="foregroundOverride">The enclosing semantic foreground, if any.</param>
    /// <returns>The text run.</returns>
    [Pure]
    public static DocumentVisualRun ForText(
        int column,
        int cells,
        int parsedRunIndex,
        int offset,
        int length,
        DocumentFaceKind face,
        int linkIndex,
        DocumentFaceKind? foregroundOverride = null) =>
        new(
            DocumentRunKind.Text,
            column,
            cells,
            parsedRunIndex,
            offset,
            length,
            default,
            null,
            face,
            foregroundOverride,
            linkIndex,
            default);

    /// <summary>Creates a run repeating one glyph across its cells.</summary>
    /// <param name="column">The zero-based start column.</param>
    /// <param name="cells">The non-negative cell width.</param>
    /// <param name="glyph">The repeated glyph.</param>
    /// <param name="face">The painting face.</param>
    /// <param name="foregroundOverride">The enclosing semantic foreground, if any.</param>
    /// <param name="semanticRange">The semantic grapheme painted by the repeat, or an empty range.</param>
    /// <param name="parsedRunIndex">The parsed run supplying style provenance, or -1.</param>
    /// <param name="offset">The UTF-16 offset whose style applies to the repeat.</param>
    /// <param name="length">The UTF-16 source length represented by the repeat.</param>
    /// <param name="linkIndex">The owning link's index, or -1.</param>
    /// <returns>The repeat run.</returns>
    [Pure]
    public static DocumentVisualRun ForRepeat(
        int column,
        int cells,
        Rune glyph,
        DocumentFaceKind face,
        DocumentFaceKind? foregroundOverride = null,
        Selection semanticRange = default,
        int parsedRunIndex = -1,
        int offset = 0,
        int length = 0,
        int linkIndex = -1) =>
        new(
            DocumentRunKind.Repeat,
            column,
            cells,
            parsedRunIndex,
            offset,
            length,
            glyph,
            null,
            face,
            foregroundOverride,
            linkIndex,
            semanticRange);

    /// <summary>Creates a positioned retained-control run.</summary>
    /// <param name="column">The zero-based start column.</param>
    /// <param name="cells">The control width.</param>
    /// <param name="control">The retained control.</param>
    /// <returns>The control run.</returns>
    [Pure]
    public static DocumentVisualRun ForControl(int column, int cells, ControlBase control) =>
        new(DocumentRunKind.Control, column, cells, -1, 0, 0, default, control, DocumentFaceKind.Body, null, -1, default);
}
