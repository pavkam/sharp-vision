// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

using SharpVision.Terminal.Input;

/// <summary>Provides immutable cell and optional pixel pointer input.</summary>
[SuppressMessage(
    "Naming",
    "CA1720:Identifier contains type name",
    Justification = "Pointer is the conventional terminal input domain term.")]
[PublicAPI]
public sealed class PointerEventArgs: RoutedEventArgs
{
    /// <summary>Initializes routed pointer input.</summary>
    /// <param name="pointer">The decoded pointer value.</param>
    public PointerEventArgs(Pointer pointer) : this(
        pointer,
        pointer.Action == PointerAction.Press ? 1 : 0)
    {
    }

    /// <summary>Initializes routed pointer input with synthesized gesture metadata.</summary>
    /// <param name="pointer">The decoded pointer value.</param>
    /// <param name="clickCount">The positive consecutive count for a press, otherwise zero.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A press has a non-positive count or a non-press has a non-zero count.
    /// </exception>
    internal PointerEventArgs(Pointer pointer, int clickCount)
    {
        if ((pointer.Action == PointerAction.Press && clickCount <= 0) ||
            (pointer.Action != PointerAction.Press && clickCount != 0))
        {
            throw new ArgumentOutOfRangeException(
                nameof(clickCount),
                clickCount,
                "Only a pointer press carries a positive click count.");
        }

        Pointer = pointer;
        ClickCount = clickCount;
        LocalCells = pointer.Cells;
    }

    /// <summary>Gets the decoded pointer value.</summary>
    public Pointer Pointer { get; }

    /// <summary>Gets the consecutive same-target press count, or zero for a non-press event.</summary>
    public int ClickCount { get; }

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
