// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

using Terminal.Rendering;

/// <summary>Renders one non-focusable, non-selectable skeleton cell standing in for an unloaded or
/// permanently failed progressive <see cref="Table"/> row.</summary>
/// <remarks>
/// Deliberately static: no timer, animation, or per-frame state beyond the themed glyph and
/// foreground themselves. A window rebuild replaces this instance once the backing range resolves,
/// so nothing here needs to observe that transition. Both the glyph and the foreground are resolved
/// fresh on every render from the owning table's own <see cref="TableStyle"/> -
/// <see cref="TableStyle.PlaceholderForeground"/> or <see cref="TableStyle.PlaceholderErrorForeground"/>
/// alongside <see cref="ControlBase.CellPolicy"/> - the same live path
/// <c>Table.ResolvedHorizontalGridGlyph</c> and its siblings already use - rather than a fixed
/// code-owned rune or hardcoded semantic color, so a theme change and cell-instance reuse across
/// derealize/realize both stay correct.
/// </remarks>
internal sealed class TablePlaceholderCell: ControlBase
{
    private readonly Table _owner;

    /// <summary>Initializes one placeholder cell.</summary>
    /// <param name="owner">The owning progressive table, used to resolve the themed glyph.</param>
    /// <param name="isError">Whether the backing range exhausted its retries.</param>
    public TablePlaceholderCell(Table owner, bool isError)
    {
        _owner = owner;
        IsError = isError;
        IsFocusable = false;
    }

    /// <summary>Gets whether the backing range exhausted its retries and shows the error glyph.</summary>
    public bool IsError { get; }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint) => new(0, 1);

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        var bounds = ContentBounds;

        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var inherited = ResolvedStyle;
        var (attributes, underline, underlineColor) = DecorationResolver.Resolve(inherited);
        var style = new TerminalStyle(
            ResolveColor(
                IsError ? _owner.ActualStyle.PlaceholderErrorForeground : _owner.ActualStyle.PlaceholderForeground,
                Theme),
            inherited.Background,
            IsError ? attributes : attributes | TerminalAttributes.Dim,
            inherited.Hyperlink,
            underline,
            underlineColor);
        var glyph = IsError ? _owner.ResolvedPlaceholderErrorGlyph : _owner.ResolvedPlaceholderGlyph;

        for (var y = bounds.Y; y < bounds.Bottom; y++)
        {
            for (var x = bounds.X; x < bounds.Right; x++)
            {
                canvas.DrawRune(glyph, new Point(x, y), style, BackgroundMode.Transparent);
            }
        }
    }
}
