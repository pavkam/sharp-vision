// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

/// <summary>Combines routed pointer input with a semantic custom-drawn marker.</summary>
internal sealed class CanvasPointerSample: Control
{
    private Point? _marker;
    private string _status = "Cells: unavailable · Pixels: unavailable";

    /// <summary>Initializes the pointer-aware drawing sample.</summary>
    internal CanvasPointerSample()
    {
        Width = Length.Cells(36);
        Height = Length.Cells(6);
        IsHitTestVisible = true;
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        return new Size(36, 6);
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

        if (eventArgs is PointerEventArgs { Pointer.Action: PointerAction.Move or PointerAction.Press } pointerEvent)
        {
            var pointer = pointerEvent.Pointer;
            _marker = pointer.Cells;
            var cells = pointer.Cells is { } cell
                ? FormattableString.Invariant($"{cell.X},{cell.Y}")
                : "unavailable";
            var pixels = pointer.Pixels is { } pixel
                ? FormattableString.Invariant($"{pixel.X},{pixel.Y}")
                : "unavailable";
            _status = $"Cells: {cells} · Pixels: {pixels}";
            Invalidate(Invalidation.Render);
        }

        base.OnEvent(eventArgs);
    }

    /// <inheritdoc/>
    protected override void OnRender(TerminalCanvas canvas)
    {
        var style = ResolvedStyle;
        canvas.Clear(Bounds, style);
        canvas.DrawBox(Bounds, LineStyle.Rounded, style);

        if (Bounds.Width < 18 || Bounds.Height < 4)
        {
            return;
        }

        _ = canvas.Draw("Move or click inside".AsSpan(), new Point(Bounds.X + 1, Bounds.Y + 1), style);
        _ = canvas.Draw(_status.AsSpan(), new Point(Bounds.X + 1, Bounds.Bottom - 2), style);

        if (_marker is { } marker && Bounds.Contains(marker))
        {
            canvas.DrawRune(new Rune('●'), marker, style);
        }
    }
}
