// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

using SharpVision.Terminal.Input;

/// <summary>Distinguishes the two conditions a <see cref="TreeViewStatusRow"/> can present.</summary>
internal enum TreeViewStatusRowKind
{
    /// <summary>A child request is in flight.</summary>
    Loading,

    /// <summary>The most recent child request failed.</summary>
    LoadFailed
}

/// <summary>
/// Renders one synthetic one-cell row standing in for an owning <see cref="TreeViewItem"/>'s
/// children while they are loading or after a load has failed. Never realized as a
/// <see cref="TreeViewItem"/> itself, so it never participates in navigation, selection, or
/// checking - <see cref="TreeView"/> excludes it from those APIs simply by filtering on type.
/// </summary>
internal sealed class TreeViewStatusRow: ControlBase
{
    private const int _rowHeightCells = 1;
    // One cell for the loading or failed indicator, then the same one-cell leading gap a
    // TreeViewItem row reserves before its header text.
    private const int _statusGlyphWidthCells = 1;
    private const int _headerLeadingSpaceCells = 1;
    // The indent span is filled on the stack up to this many cells; a deeper nesting than this
    // falls back to a heap array instead, so an adversarially deep tree cannot blow the stack.
    private const int _indentStackAllocLimitCells = 256;
    private readonly TreeViewItem _owner;

    /// <summary>Initializes a status row owned by one tree view item.</summary>
    /// <param name="owner">The non-null item this row stands in for.</param>
    internal TreeViewStatusRow(TreeViewItem owner)
    {
        _owner = owner;
        Height = Length.Cells(_rowHeightCells);
        HorizontalAlignment = HorizontalAlignment.Stretch;
        IsFocusable = false;
        IsTabStop = false;
    }

    /// <summary>Gets or sets which condition this row currently presents.</summary>
    internal TreeViewStatusRowKind Kind { get; set; }

    /// <summary>Gets or sets the nesting depth, set by the owning tree view on every flatten.</summary>
    internal int Depth { get; set; }

    private string StatusText
    {
        get
        {
            var tree = _owner.FindTreeView();

            return Kind == TreeViewStatusRowKind.LoadFailed
                ? tree?.LoadFailedText ?? string.Empty
                : tree?.LoadingText ?? string.Empty;
        }
    }

    private TreeViewStyle StatusStyle => _owner.FindTreeView()?.ActualStyle ?? TreeViewStyle.Default;

    private Rune StatusGlyph => Kind == TreeViewStatusRowKind.LoadFailed
        ? StatusStyle.FailedGlyph
        : StatusStyle.LoadingGlyph;

    // The code-owned portable repair value for whichever glyph StatusGlyph currently reads - the
    // same registry TreeViewStyle.Complete seeded the themable value from in the first place.
    private Rune StatusGlyphFallback => Kind == TreeViewStatusRowKind.LoadFailed
        ? ControlGlyphs.Status.Failed.Fallback
        : ControlGlyphs.Status.Loading.Fallback;

    private ControlColor StatusColor => Kind == TreeViewStatusRowKind.LoadFailed
        ? StatusStyle.FailedColor
        : StatusStyle.LoadingColor;

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        var indent = _owner.FindTreeView()?.Indent ?? 2;

        return new Size(
            (int) Math.Min(
                int.MaxValue,
                ((long) Depth * indent) +
                _statusGlyphWidthCells +
                _headerLeadingSpaceCells +
                MeasureCells(StatusText)),
            _rowHeightCells);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        var style = ResolvedStyle;

        if (this.HasOpaqueFill(GetAppearanceState()))
        {
            canvas.Clear(Bounds, style);
        }

        var bounds = ContentBounds;

        if (bounds.Width == 0 || bounds.Height == 0)
        {
            return;
        }

        var statusStyle = style.WithForeground(ResolveColor(StatusColor));
        var indent = _owner.FindTreeView()?.Indent ?? 2;
        var indentCells = Depth * indent;
        var clipped = canvas.Clip(bounds);
        var x = bounds.X;

        if (indentCells > 0)
        {
            var indentSpan = indentCells <= _indentStackAllocLimitCells
                ? stackalloc char[indentCells]
                : new char[indentCells];
            indentSpan.Fill(' ');
            var leading = clipped.Draw(
                indentSpan,
                new Point(x, bounds.Y),
                style,
                background: BackgroundMode.Transparent);
            x = leading.Final.X;
        }

        canvas.DrawRune(
            StatusGlyph.Resolve(StatusGlyphFallback, CellPolicy.AmbiguousWidth),
            new Point(x, bounds.Y),
            statusStyle,
            BackgroundMode.Transparent);
        x += _statusGlyphWidthCells;

        if (bounds.Width > x - bounds.X)
        {
            var textClipped = canvas.Clip(new Rect(x, bounds.Y, bounds.Width - (x - bounds.X), _rowHeightCells));
            var leading = textClipped.Draw(
                " ".AsSpan(),
                new Point(x, bounds.Y),
                statusStyle,
                background: BackgroundMode.Transparent);
            _ = textClipped.Draw(
                StatusText.AsSpan(),
                new Point(leading.Final.X, bounds.Y),
                statusStyle,
                background: BackgroundMode.Transparent);
        }
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

        base.OnEvent(eventArgs);

        if (eventArgs.IsHandled)
        {
            return;
        }

        if (Kind == TreeViewStatusRowKind.LoadFailed &&
            eventArgs is PointerEventArgs
            {
                Pointer.Action: PointerAction.Press,
                Pointer.Buttons: var buttons,
                Pointer.Cells: { } cells
            } &&
            (buttons & Buttons.Primary) != 0 &&
            Bounds.Contains(cells))
        {
            _ = _owner.ReloadChildrenAsync();
            eventArgs.IsHandled = true;
        }
    }
}
