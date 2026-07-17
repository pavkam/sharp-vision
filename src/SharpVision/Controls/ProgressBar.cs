// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Displays a visual progress indicator using block characters with optional sub-cell resolution.</summary>
public sealed class ProgressBar: Control
{
    private static readonly string[] _horizontalBlocks = [" ", "▏", "▎", "▍", "▌", "▋", "▊", "▉", "█"];
    private static readonly string[] _verticalBlocks = [" ", "▁", "▂", "▃", "▄", "▅", "▆", "▇", "█"];

    /// <summary>Initializes a non-focusable horizontal progress bar at zero progress.</summary>
    public ProgressBar() => IsHitTestVisible = false;

    /// <summary>Gets or sets the minimum value.</summary>
    /// <exception cref="InvalidOperationException">The attached progress bar is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The progress bar is disposed.</exception>
    public double Minimum
    {
        get;
        set => _ = SetProperty(ref field, value, ChangeImpact.Render);
    }

    /// <summary>Gets or sets the maximum value.</summary>
    /// <exception cref="InvalidOperationException">The attached progress bar is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The progress bar is disposed.</exception>
    public double Maximum
    {
        get;
        set => _ = SetProperty(ref field, value, ChangeImpact.Render);
    } = 1.0;

    /// <summary>Gets or sets the current value, clamped between Minimum and Maximum.</summary>
    /// <exception cref="InvalidOperationException">The attached progress bar is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The progress bar is disposed.</exception>
    public double Value
    {
        get;
        set
        {
            var clamped = Math.Clamp(value, Minimum, Maximum);
            _ = SetProperty(ref field, clamped, ChangeImpact.Render);
        }
    }

    /// <summary>Gets or sets whether the bar shows an indeterminate state.</summary>
    /// <exception cref="InvalidOperationException">The attached progress bar is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The progress bar is disposed.</exception>
    public bool IsIndeterminate
    {
        get;
        set => _ = SetProperty(ref field, value, ChangeImpact.Render);
    }

    /// <summary>Gets or sets horizontal or vertical bar layout.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached progress bar is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The progress bar is disposed.</exception>
    public Orientation Orientation
    {
        get;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The progress bar orientation is unknown.");
            }

            _ = SetProperty(ref field, value, ChangeImpact.Measure);
        }
    } = Orientation.Horizontal;

    /// <summary>Gets or sets whether to use fractional block characters for sub-cell resolution.</summary>
    /// <remarks>
    /// When true, horizontal bars use ▏▎▍▌▋▊▉█ (8 levels per cell) and vertical bars use
    /// ▁▂▃▄▅▆▇█ (8 levels per cell), providing 8x the effective resolution.
    /// When false, each cell is either fully filled (█) or empty (░).
    /// </remarks>
    /// <exception cref="InvalidOperationException">The attached progress bar is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The progress bar is disposed.</exception>
    public bool UseSubCellResolution
    {
        get;
        set => _ = SetProperty(ref field, value, ChangeImpact.Render);
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        return Orientation == Orientation.Horizontal ? new Size(10, 1) : new Size(1, 10);
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        if (Bounds.Width == 0 || Bounds.Height == 0)
        {
            return;
        }

        var style = ResolvedStyle;
        var range = Maximum - Minimum;
        var ratio = range > 0 ? Math.Clamp((Value - Minimum) / range, 0, 1) : 0;

        if (Orientation == Orientation.Horizontal)
        {
            RenderHorizontal(canvas, style, ratio);
        }
        else
        {
            RenderVertical(canvas, style, ratio);
        }
    }

    private void RenderHorizontal(TerminalCanvas canvas, TerminalStyle style, double ratio)
    {
        if (UseSubCellResolution)
        {
            var totalEighths = (int) (ratio * Bounds.Width * 8);
            var fullCells = totalEighths / 8;
            var remainder = totalEighths % 8;

            for (var x = Bounds.X; x < Bounds.Right; x++)
            {
                var cellIndex = x - Bounds.X;
                var glyph = cellIndex < fullCells
                    ? _horizontalBlocks[8]
                    : cellIndex == fullCells && remainder > 0
                        ? _horizontalBlocks[remainder]
                        : " ";
                _ = canvas.Draw(glyph.AsSpan(), new Point(x, Bounds.Y), style, background: BackgroundMode.Transparent);
            }
        }
        else
        {
            var filled = (int) (Bounds.Width * ratio);

            for (var x = Bounds.X; x < Bounds.Right; x++)
            {
                var glyph = x - Bounds.X < filled ? "█" : "░";
                _ = canvas.Draw(glyph.AsSpan(), new Point(x, Bounds.Y), style, background: BackgroundMode.Transparent);
            }
        }
    }

    private void RenderVertical(TerminalCanvas canvas, TerminalStyle style, double ratio)
    {
        if (UseSubCellResolution)
        {
            var totalEighths = (int) (ratio * Bounds.Height * 8);
            var fullCells = totalEighths / 8;
            var remainder = totalEighths % 8;

            for (var y = Bounds.Y; y < Bounds.Bottom; y++)
            {
                var cellFromBottom = Bounds.Bottom - 1 - y;
                var glyph = cellFromBottom < fullCells
                    ? _verticalBlocks[8]
                    : cellFromBottom == fullCells && remainder > 0
                        ? _verticalBlocks[remainder]
                        : " ";
                _ = canvas.Draw(glyph.AsSpan(), new Point(Bounds.X, y), style, background: BackgroundMode.Transparent);
            }
        }
        else
        {
            var filled = (int) (Bounds.Height * ratio);
            var emptyEnd = Bounds.Bottom - filled;

            for (var y = Bounds.Y; y < Bounds.Bottom; y++)
            {
                var glyph = y >= emptyEnd ? "█" : "░";
                _ = canvas.Draw(glyph.AsSpan(), new Point(Bounds.X, y), style, background: BackgroundMode.Transparent);
            }
        }
    }
}
