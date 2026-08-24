// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Documents;

/// <summary>Represents one indivisible unit of inline content during line breaking.</summary>
/// <remarks>
/// Tokens come from every inline in a paragraph, so a line can break between two runs, between a run
/// and a link, or inside one run - wherever the nearest whitespace boundary falls.
/// </remarks>
internal readonly struct DocumentFlowToken
{
    private DocumentFlowToken(
        DocumentFlowTokenKind kind,
        int parsedRunIndex,
        int offset,
        int length,
        int cells,
        Rune glyph,
        ControlBase? control,
        DocumentFaceKind face,
        int linkIndex,
        Selection semanticRange)
    {
        Kind = kind;
        ParsedRunIndex = parsedRunIndex;
        Offset = offset;
        Length = length;
        Cells = cells;
        Glyph = glyph;
        Control = control;
        Face = face;
        LinkIndex = linkIndex;
        SemanticRange = semanticRange;
    }

    /// <summary>Gets what this token contributes to the line.</summary>
    public DocumentFlowTokenKind Kind { get; }

    /// <summary>Gets the parsed-run index supplying the display text, or -1.</summary>
    public int ParsedRunIndex { get; }

    /// <summary>Gets the UTF-16 offset into the parsed run's display text.</summary>
    public int Offset { get; }

    /// <summary>Gets the UTF-16 length within the parsed run's display text.</summary>
    public int Length { get; }

    /// <summary>Gets the non-negative cell width this token occupies.</summary>
    public int Cells { get; }

    /// <summary>Gets the glyph a blank token repeats.</summary>
    public Rune Glyph { get; }

    /// <summary>Gets the embedded control for a control token, or null.</summary>
    public ControlBase? Control { get; }

    /// <summary>Gets which style face paints this token.</summary>
    public DocumentFaceKind Face { get; }

    /// <summary>Gets the owning link's index, or -1 when this token is not part of a link.</summary>
    public int LinkIndex { get; }

    /// <summary>Gets the semantic range painted by a generated blank, or an empty range.</summary>
    public Selection SemanticRange { get; }

    /// <summary>Creates a word or whitespace token slicing parsed display text.</summary>
    /// <param name="kind">Either <see cref="DocumentFlowTokenKind.Word"/> or <see cref="DocumentFlowTokenKind.Space"/>.</param>
    /// <param name="parsedRunIndex">The parsed-run index supplying the display text.</param>
    /// <param name="offset">The UTF-16 offset into the display text.</param>
    /// <param name="length">The UTF-16 length within the display text.</param>
    /// <param name="cells">The non-negative cell width.</param>
    /// <param name="face">The painting face.</param>
    /// <param name="linkIndex">The owning link's index, or -1.</param>
    /// <returns>The token.</returns>
    [Pure]
    public static DocumentFlowToken ForText(
        DocumentFlowTokenKind kind,
        int parsedRunIndex,
        int offset,
        int length,
        int cells,
        DocumentFaceKind face,
        int linkIndex) =>
        new(kind, parsedRunIndex, offset, length, cells, default, null, face, linkIndex, default);

    /// <summary>Creates a whitespace token that advances without drawing text, used for an expanded
    /// tab.</summary>
    /// <param name="cells">The positive cell advance.</param>
    /// <param name="face">The painting face.</param>
    /// <param name="linkIndex">The owning link's index, or -1.</param>
    /// <param name="semanticRange">The grapheme range represented by the blank, or an empty range.</param>
    /// <returns>The token.</returns>
    [Pure]
    public static DocumentFlowToken ForBlank(
        int cells,
        DocumentFaceKind face,
        int linkIndex,
        Selection semanticRange = default) =>
        new(DocumentFlowTokenKind.Space, -1, 0, 0, cells, new Rune(' '), null, face, linkIndex, semanticRange);

    /// <summary>Creates an atomic embedded-control token.</summary>
    /// <param name="control">The measured single-line control.</param>
    /// <param name="cells">Its non-negative cell width.</param>
    /// <returns>The token.</returns>
    [Pure]
    public static DocumentFlowToken ForControl(ControlBase control, int cells) =>
        new(DocumentFlowTokenKind.Control, -1, 0, 0, cells, default, control, DocumentFaceKind.Body, -1, default);

    /// <summary>Creates a hard line break token.</summary>
    /// <returns>The token.</returns>
    [Pure]
    public static DocumentFlowToken ForBreak() =>
        new(DocumentFlowTokenKind.Break, -1, 0, 0, 0, default, null, DocumentFaceKind.Body, -1, default);
}
