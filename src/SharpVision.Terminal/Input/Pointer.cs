// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Input;


/// <summary>Represents immutable cell and optional pixel pointer input.</summary>
[SuppressMessage(
    "Naming",
    "CA1720:Identifier contains type name",
    Justification = "Pointer is the conventional terminal input domain term.")]
[PublicAPI]
public readonly record struct Pointer
{
    private const Buttons _allButtons =
        Buttons.Primary |
        Buttons.Middle |
        Buttons.Secondary |
        Buttons.Back |
        Buttons.Forward;

    private const Modifiers _allModifiers =
        Modifiers.Shift |
        Modifiers.Alt |
        Modifiers.Control |
        Modifiers.Super |
        Modifiers.Hyper |
        Modifiers.Meta |
        Modifiers.CapsLock |
        Modifiers.NumLock;

    /// <summary>Initializes a validated pointer value.</summary>
    /// <param name="cells">The optional non-negative zero-based cell position.</param>
    /// <param name="pixels">The optional non-negative zero-based pixel position.</param>
    /// <param name="buttons">The active or transitioning buttons.</param>
    /// <param name="action">The pointer action.</param>
    /// <param name="wheelX">The horizontal wheel delta.</param>
    /// <param name="wheelY">The vertical wheel delta.</param>
    /// <param name="modifiers">The active modifiers.</param>
    /// <param name="isMotion">Whether the wire event explicitly reported motion.</param>
    /// <param name="isCellPositionInferred">Whether cells were derived from pixels.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A coordinate, enum, button, or modifier value is invalid.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Coordinates are missing outside leave, leave carries coordinates, or
    /// inferred cells are requested without both coordinate families.
    /// </exception>
    public Pointer(
        Point? cells,
        Point? pixels,
        Buttons buttons,
        PointerAction action,
        int wheelX,
        int wheelY,
        Modifiers modifiers,
        bool isMotion,
        bool isCellPositionInferred)
    {
        if (cells is { } cellPosition)
        {
            ValidatePoint(cellPosition, nameof(cells));
        }

        if (pixels is { } pixelPosition)
        {
            ValidatePoint(pixelPosition, nameof(pixels));
        }

        // Deliberately NOT ArgumentOutOfRangeException.ThrowIfUndefinedFlags for either flags
        // guard below: a Pointer is constructed for every decoded pointer event, and the generic
        // helper's Convert.ToUInt64 boxes both operands on every call. The direct bitwise checks
        // cost nothing since Buttons' and Modifiers' operators never box.
        if ((buttons & ~_allButtons) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(buttons), buttons, "The pointer button set contains unknown flags.");
        }

        ArgumentOutOfRangeException.ThrowIfNotDefined(action, nameof(action), "The pointer action is unknown.");

        if ((modifiers & ~_allModifiers) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(modifiers), modifiers, "The modifier set contains unknown flags.");
        }

        if (action == PointerAction.Leave && (cells is not null || pixels is not null))
        {
            throw new ArgumentException(
                "Pointer leave cannot carry cell or pixel coordinates.",
                nameof(action));
        }

        if (action is PointerAction.Press or PointerAction.Release or PointerAction.Move &&
            cells is null &&
            pixels is null)
        {
            throw new ArgumentException(
                "Press, release, and move input require cells, pixels, or both.",
                nameof(cells));
        }

        if (isCellPositionInferred && (cells is null || pixels is null))
        {
            throw new ArgumentException(
                "Inferred cell coordinates require cell and pixel positions.",
                nameof(isCellPositionInferred));
        }

        Cells = cells;
        Pixels = pixels;
        Buttons = buttons;
        Action = action;
        WheelX = wheelX;
        WheelY = wheelY;
        Modifiers = modifiers;
        MotionReported = isMotion;
        CellPositionInferred = isCellPositionInferred;
    }

    /// <summary>Gets the optional zero-based cell position.</summary>
    public Point? Cells { get; }

    /// <summary>Gets the optional zero-based pixel position.</summary>
    public Point? Pixels { get; }

    /// <summary>Gets the active or transitioning buttons.</summary>
    public Buttons Buttons { get; }

    /// <summary>Gets the pointer action.</summary>
    public PointerAction Action { get; }

    /// <summary>Gets the horizontal wheel delta.</summary>
    public int WheelX { get; }

    /// <summary>Gets the vertical wheel delta.</summary>
    public int WheelY { get; }

    /// <summary>Gets active modifiers.</summary>
    public Modifiers Modifiers { get; }

    /// <summary>Gets whether the wire event explicitly reported motion.</summary>
    public bool MotionReported { get; }

    /// <summary>Gets whether cells were derived from pixels.</summary>
    public bool CellPositionInferred { get; }

    private static void ValidatePoint(Point value, string parameterName)
    {
        if (value.X < 0 || value.Y < 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Pointer coordinates cannot be negative.");
        }
    }
}
