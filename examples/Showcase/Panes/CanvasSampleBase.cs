// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

/// <summary>
/// Shared base for the fixed-size Canvas showcase specimens. Fixes the control's size,
/// implements the common clear + border + minimum-size-guard sequence every specimen's
/// <see cref="OnRenderContent"/> needs, then hands off to <see cref="DrawContent"/> for the
/// specimen's own unique drawing.
/// </summary>
internal abstract class CanvasSampleBase: ControlBase
{
    private readonly Size _size;
    private readonly int _minWidth;
    private readonly int _minHeight;
    private readonly LineStyle _borderStyle;

    /// <summary>Initializes the fixed size, minimum content size, and border style shared by every specimen.</summary>
    protected CanvasSampleBase(int width, int height, int minWidth, int minHeight, LineStyle borderStyle)
    {
        Width = Length.Cells(width);
        Height = Length.Cells(height);
        _size = new Size(width, height);
        _minWidth = minWidth;
        _minHeight = minHeight;
        _borderStyle = borderStyle;
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        return _size;
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        var style = ResolvedStyle;
        canvas.Clear(Bounds, style);
        canvas.DrawBox(Bounds, _borderStyle, style);

        if (Bounds.Width < _minWidth || Bounds.Height < _minHeight)
        {
            return;
        }

        DrawContent(canvas, style);
    }

    /// <summary>Draws the specimen's unique content inside the border and guard established by the base class.</summary>
    protected abstract void DrawContent(TerminalCanvas canvas, CellStyle style);
}
