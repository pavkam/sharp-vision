// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;


using SharpVision.Terminal.Input;

/// <summary>Provides immutable cell and optional pixel pointer input.</summary>
[SuppressMessage(
    "Naming",
    "CA1720:Identifier contains type name",
    Justification = "Pointer is the conventional terminal input domain term.")]
public sealed class PointerEventArgs: RoutedEventArgs
{
    /// <summary>Initializes routed pointer input.</summary>
    /// <param name="pointer">The decoded pointer value.</param>
    public PointerEventArgs(Pointer pointer)
    {
        Pointer = pointer;
        LocalCells = pointer.Cells;
    }

    /// <summary>Gets the decoded pointer value.</summary>
    public Pointer Pointer { get; }

    /// <summary>Gets available screen cells relative to the active handler control.</summary>
    public Point? LocalCells { get; private set; }

    /// <summary>Updates local coordinates for one route element.</summary>
    internal void SetLocal(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        LocalCells = Pointer.Cells is { } cells
            ? new Point(
                Difference(cells.X, control.Bounds.X),
                Difference(cells.Y, control.Bounds.Y))
            : null;
    }

    private static int Difference(int left, int right)
    {
        var result = (long) left - right;
        return (int) Math.Clamp(result, int.MinValue, int.MaxValue);
    }
}
