using System.Diagnostics.CodeAnalysis;

using SharpVision.Controls;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Input;

namespace SharpVision.Input;

/// <summary>Provides immutable cell and optional pixel pointer input.</summary>
/// <param name="pointer">The decoded pointer value.</param>
[SuppressMessage(
    "Naming",
    "CA1720:Identifier contains type name",
    Justification = "Pointer is the conventional terminal input domain term.")]
public sealed class PointerEventArgs(Pointer pointer): RoutedEventArgs
{
    /// <summary>Gets the decoded pointer value.</summary>
    public Pointer Pointer { get; } = pointer;

    /// <summary>Gets current screen cells relative to the active handler control.</summary>
    public Point LocalCells { get; private set; } = pointer.Cells;

    /// <summary>Updates local coordinates for one route element.</summary>
    internal void SetLocal(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        LocalCells = new Point(
            Difference(Pointer.Cells.X, control.Bounds.X),
            Difference(Pointer.Cells.Y, control.Bounds.Y));
    }

    private static int Difference(int left, int right)
    {
        var result = (long) left - right;
        return (int) Math.Clamp(result, int.MinValue, int.MaxValue);
    }
}
