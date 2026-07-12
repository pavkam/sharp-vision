using SharpVision.Controls;
using SharpVision.Input;
using SharpVision.Layout;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Input;

using TerminalCanvas = SharpVision.Terminal.Rendering.Canvas;

namespace SharpVision.Showcase;

/// <summary>Displays live pixel and optional mapped-cell coordinates for pointer input.</summary>
internal sealed class PointerProbe: Control
{
    private string _pixelText = "Pixels: unavailable";
    private string _cellText = "Cells: unavailable";

    /// <summary>Initializes the readout surface.</summary>
    internal PointerProbe()
    {
        Width = Length.Cells(42);
        Height = Length.Cells(2);
        IsHitTestVisible = true;
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);

        if (eventArgs is PointerEventArgs { Pointer.Action: PointerAction.Move or PointerAction.Press } routed)
        {
            Update(routed.Pointer);
            Invalidate(Invalidation.Render);
        }

        base.OnEvent(eventArgs);
    }

    /// <inheritdoc/>
    protected override void RenderCore(TerminalCanvas canvas)
    {
        var style = ResolvedStyle;
        canvas.Clear(Bounds, style);
        _ = canvas.Draw(_pixelText.AsSpan(), new Point(Bounds.X, Bounds.Y), style);
        _ = canvas.Draw(_cellText.AsSpan(), new Point(Bounds.X, Bounds.Y + 1), style);
    }

    private void Update(Pointer pointer)
    {
        _pixelText = pointer.Pixels is { } pixels
            ? FormattableString.Invariant($"Pixels: {pixels.X},{pixels.Y}")
            : "Pixels: unavailable";
        _cellText = pointer.Cells is { } cells
            ? FormattableString.Invariant($"Cells: {cells.X},{cells.Y}")
            : "Cells: unavailable";
    }
}
