// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Document.Tests;

using SharpVision.Text;

/// <summary>Provides deterministic selectable text and viewport identity for document-map tests.</summary>
internal sealed class DocumentSelectionSourceProbe: ControlBase, ISelectableTextSource, ISelectableTextViewport
{
    /// <summary>Gets the committed probe-only horizontal offset.</summary>
    internal int HorizontalOffset { get; set; }

    /// <summary>Gets the committed probe-only vertical offset.</summary>
    internal int VerticalOffset { get; set; }

    /// <summary>Gets or sets whether the snapshot models a horizontally scrollable ten-cell line.</summary>
    internal bool UsesHorizontalProjection { get; set; }

    /// <summary>Gets or sets whether the snapshot models two vertically scrollable five-cell lines.</summary>
    internal bool UsesVerticalProjection { get; set; }

    /// <summary>Gets or sets the greatest accepted horizontal offset.</summary>
    internal int MaximumHorizontalOffset { get; set; }

    /// <summary>Gets or sets the greatest accepted vertical offset.</summary>
    internal int MaximumVerticalOffset { get; set; }

    /// <summary>Gets or sets whether the source changes its semantic text after receiving bounds.</summary>
    internal bool ChangesTextAfterArrange { get; set; }

    /// <summary>Gets or sets an optional first-glyph rectangle for overflow translation tests.</summary>
    internal Rect? FirstGlyphBoundsOverride { get; set; }

    /// <summary>Gets the most recent semantic offset offered for keyboard reveal.</summary>
    internal int? RevealedOffset { get; private set; }

    /// <summary>Gets or sets a synchronous callback invoked during keyboard reveal.</summary>
    internal Action? RevealAction { get; set; }

    /// <inheritdoc/>
    public Rect SelectableTextViewport => new(0, 0, 5, 1);

    /// <inheritdoc/>
    public SelectableTextSnapshot GetSelectableTextSnapshot()
    {
        if (UsesHorizontalProjection)
        {
            const string horizontalText = "ProbeNext";
            var first = Math.Min(HorizontalOffset, horizontalText.Length);
            var last = Math.Min(horizontalText.Length, first + 5);
            var glyphs = new List<SelectableTextGlyph>(last - first);

            for (var offset = first; offset < last; offset++)
            {
                glyphs.Add(new SelectableTextGlyph(
                    new Selection(offset, offset + 1),
                    new Rect(offset - first, 0, 1, 1)));
            }

            return new SelectableTextSnapshot(horizontalText, glyphs, isAuthoritative: true);
        }

        if (UsesVerticalProjection)
        {
            const string verticalText = "Probe\nNext";
            var first = VerticalOffset == 0 ? 0 : 6;
            var length = VerticalOffset == 0 ? 5 : 4;
            var glyphs = new List<SelectableTextGlyph>(length);

            for (var index = 0; index < length; index++)
            {
                glyphs.Add(new SelectableTextGlyph(
                    new Selection(first + index, first + index + 1),
                    new Rect(index, 0, 1, 1)));
            }

            return new SelectableTextSnapshot(verticalText, glyphs, isAuthoritative: true);
        }

        var text = ChangesTextAfterArrange && Bounds.Width > 0 ? "After" : "Probe";
        return new SelectableTextSnapshot(
            text,
            [
                new SelectableTextGlyph(
                    new Selection(0, 1),
                    FirstGlyphBoundsOverride ?? new Rect(0, 0, 1, 1)),
                new SelectableTextGlyph(new Selection(1, 2), new Rect(1, 0, 1, 1)),
                new SelectableTextGlyph(new Selection(2, 3), new Rect(2, 0, 1, 1)),
                new SelectableTextGlyph(new Selection(3, 4), new Rect(3, 0, 1, 1)),
                new SelectableTextGlyph(new Selection(4, 5), new Rect(4, 0, 1, 1))
            ],
            isAuthoritative: true);
    }

    /// <inheritdoc/>
    public bool RevealSelectableTextOffset(int offset)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset, GetSelectableTextSnapshot().Text.Length);

        RevealedOffset = offset;
        RevealAction?.Invoke();

        return false;
    }

    /// <inheritdoc/>
    public bool ScrollSelectableTextViewport(int horizontal, int vertical)
    {
        var nextHorizontal = Math.Clamp(HorizontalOffset.Add(horizontal), 0, MaximumHorizontalOffset);
        var nextVertical = Math.Clamp(VerticalOffset.Add(vertical), 0, MaximumVerticalOffset);

        if (nextHorizontal == HorizontalOffset && nextVertical == VerticalOffset)
        {
            return false;
        }

        HorizontalOffset = nextHorizontal;
        VerticalOffset = nextVertical;
        Invalidate();
        return true;
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        return new Size(5, 1);
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas) =>
        _ = canvas.Draw("Probe", new Point(ContentBounds.X, ContentBounds.Y), default);
}
