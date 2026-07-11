using System.Diagnostics.CodeAnalysis;

using SharpVision.Terminal.Geometry;

namespace SharpVision.Terminal.Input;

/// <summary>Identifies logical pointer buttons.</summary>
[Flags]
public enum Buttons
{
    /// <summary>No button is active.</summary>
    None = 0,

    /// <summary>The primary button.</summary>
    Primary = 1 << 0,

    /// <summary>The middle button.</summary>
    Middle = 1 << 1,

    /// <summary>The secondary button.</summary>
    Secondary = 1 << 2,

    /// <summary>The first extended button.</summary>
    Back = 1 << 3,

    /// <summary>The second extended button.</summary>
    Forward = 1 << 4,
}

/// <summary>Identifies pointer movement, buttons, scrolling, and leave.</summary>
public enum PointerAction
{
    /// <summary>The pointer moved without a button transition.</summary>
    Move,

    /// <summary>A button was pressed.</summary>
    Press,

    /// <summary>A button was released.</summary>
    Release,

    /// <summary>A wheel changed.</summary>
    Wheel,

    /// <summary>The pointer left the tracked surface.</summary>
    Leave,
}

/// <summary>Represents immutable cell and optional pixel pointer input.</summary>
[SuppressMessage(
    "Naming",
    "CA1720:Identifier contains type name",
    Justification = "Pointer is the conventional terminal input domain term.")]
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
    /// <param name="cells">The non-negative zero-based cell position.</param>
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
    /// Inferred cells are requested without a pixel position.
    /// </exception>
    public Pointer(
        Point cells,
        Point? pixels,
        Buttons buttons,
        PointerAction action,
        int wheelX,
        int wheelY,
        Modifiers modifiers,
        bool isMotion,
        bool isCellPositionInferred)
    {
        ValidatePoint(cells, nameof(cells));

        if (pixels is { } pixelPosition)
        {
            ValidatePoint(pixelPosition, nameof(pixels));
        }

        if ((buttons & ~_allButtons) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(buttons),
                buttons,
                "The pointer button set contains unknown flags.");
        }

        if (!Enum.IsDefined(action))
        {
            throw new ArgumentOutOfRangeException(
                nameof(action),
                action,
                "The pointer action is unknown.");
        }

        if ((modifiers & ~_allModifiers) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(modifiers),
                modifiers,
                "The modifier set contains unknown flags.");
        }

        if (isCellPositionInferred && pixels is null)
        {
            throw new ArgumentException(
                "Inferred cell coordinates require a pixel position.",
                nameof(isCellPositionInferred));
        }

        Cells = cells;
        Pixels = pixels;
        Buttons = buttons;
        Action = action;
        WheelX = wheelX;
        WheelY = wheelY;
        Modifiers = modifiers;
        IsMotion = isMotion;
        IsCellPositionInferred = isCellPositionInferred;
    }

    /// <summary>Gets the zero-based cell position.</summary>
    public Point Cells { get; }

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
    public bool IsMotion { get; }

    /// <summary>Gets whether cells were derived from pixels.</summary>
    public bool IsCellPositionInferred { get; }

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
