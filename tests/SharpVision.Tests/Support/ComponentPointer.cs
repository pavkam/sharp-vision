// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Drives a mounted component with real SGR terminal pointer reports.</summary>
internal sealed class ComponentPointer
{
    private readonly ComponentSurface _surface;
    private Point? _lastPoint;
    private Modifiers _primaryModifiers;
    private bool _primaryPressed;

    /// <summary>Initializes a pointer driver for one non-null mounted surface.</summary>
    /// <param name="surface">The owning component surface.</param>
    /// <exception cref="ArgumentNullException"><paramref name="surface"/> is null.</exception>
    internal ComponentPointer(ComponentSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        _surface = surface;
    }

    /// <summary>Moves the pointer to the current arranged center of an owned control.</summary>
    /// <param name="control">The mounted control or one of its owned descendants.</param>
    /// <returns>A task completed after hover and rendering settle.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="control"/> is not owned by the surface.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="control"/> has empty arranged bounds.</exception>
    internal async Task MoveToAsync(Control control)
    {
        var point = await _surface.ResolvePointAsync(control);
        var value = Encode(button: 35, point, final: 'M');
        await _surface.SendAsync(value, $"move pointer to {point}");
        _lastPoint = point;
    }

    /// <summary>Moves the pointer to one cell relative to an owned control's arranged bounds.</summary>
    /// <param name="control">The mounted control or one of its owned descendants.</param>
    /// <param name="relative">The zero-based cell inside the control.</param>
    /// <returns>A task completed after hover and rendering settle.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="control"/> is not owned by the surface.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="control"/> has empty arranged bounds.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="relative"/> lies outside the control.</exception>
    internal async Task MoveToAsync(Control control, Point relative)
    {
        var point = await _surface.ResolvePointAsync(control, relative);
        await _surface.SendAsync(Encode(button: 35, point, final: 'M'), $"move pointer to {point}");
        _lastPoint = point;
    }

    /// <summary>Holds the primary pointer button at the most recently moved-to point.</summary>
    /// <returns>A task completed after focus, capture, pressed state, and rendering settle.</returns>
    /// <exception cref="InvalidOperationException">The pointer has not been positioned or is already pressed.</exception>
    internal async Task PressAsync()
    {
        if (_primaryPressed)
        {
            throw new InvalidOperationException("The primary pointer button is already pressed.");
        }

        var point = _lastPoint ?? throw new InvalidOperationException(
            "Move the component pointer before pressing a button.");
        await _surface.SendAsync(Encode(button: 0, point, final: 'M'), $"press primary pointer at {point}");
        _primaryModifiers = Modifiers.None;
        _primaryPressed = true;
    }

    /// <summary>Releases the primary pointer button at the most recently moved-to point.</summary>
    /// <returns>A task completed after capture release, activation, and rendering settle.</returns>
    /// <exception cref="InvalidOperationException">The primary button is not currently pressed.</exception>
    internal async Task ReleaseAsync()
    {
        if (!_primaryPressed)
        {
            throw new InvalidOperationException("Press the primary pointer button before releasing it.");
        }

        var point = _lastPoint ?? throw new InvalidOperationException(
            "Move the component pointer before releasing a button.");
        await _surface.SendAsync(
            Encode(button: ModifierBits(_primaryModifiers), point, final: 'm'),
            $"release primary pointer at {point}");
        _primaryModifiers = Modifiers.None;
        _primaryPressed = false;
    }

    /// <summary>Moves to and clicks the current arranged center of an owned control.</summary>
    /// <param name="control">The mounted control or one of its owned descendants.</param>
    /// <returns>A task completed after move, press, release, activation, and rendering settle.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="control"/> is not owned by the surface.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="control"/> has empty arranged bounds.</exception>
    internal async Task ClickAsync(Control control)
    {
        await MoveToAsync(control);
        await PressAsync();
        await ReleaseAsync();
    }

    /// <summary>Clicks one cell relative to an owned control's arranged bounds.</summary>
    /// <param name="control">The mounted control or one of its owned descendants.</param>
    /// <param name="relative">The zero-based cell inside the control.</param>
    /// <returns>A task completed after move, press, release, activation, and rendering settle.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="control"/> is not owned by the surface.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="control"/> has empty arranged bounds.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="relative"/> lies outside the control.</exception>
    internal async Task ClickAsync(Control control, Point relative)
    {
        await MoveToAsync(control, relative);
        await PressAsync();
        await ReleaseAsync();
    }

    /// <summary>Clicks a control's current arranged center with supported terminal pointer modifiers.</summary>
    /// <param name="control">The mounted control or one of its owned descendants.</param>
    /// <param name="modifiers">The Shift, Alt, and Control flags carried by both pointer transitions.</param>
    /// <returns>A task completed after move, modified press/release, activation, and rendering settle.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="control"/> is not owned by the surface.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="modifiers"/> contains an undefined flag.</exception>
    /// <exception cref="NotSupportedException"><paramref name="modifiers"/> contains a defined non-pointer modifier.</exception>
    /// <exception cref="InvalidOperationException">The control is empty or the primary pointer is already pressed.</exception>
    internal async Task ClickAsync(Control control, Modifiers modifiers)
    {
        var bits = ModifierBits(modifiers);
        await MoveToAsync(control);

        if (_primaryPressed)
        {
            throw new InvalidOperationException("The primary pointer button is already pressed.");
        }

        var point = _lastPoint!.Value;
        await _surface.SendAsync(Encode(button: bits, point, final: 'M'), $"press modified primary pointer at {point}");
        _primaryModifiers = modifiers;
        _primaryPressed = true;
        await ReleaseAsync();
    }

    /// <summary>Emits one unit horizontal or vertical wheel report at a control-relative cell.</summary>
    /// <param name="control">The mounted control or one of its owned descendants.</param>
    /// <param name="relative">The zero-based cell inside the control.</param>
    /// <param name="wheelX">Horizontal unit delta from -1 through 1.</param>
    /// <param name="wheelY">Vertical unit delta from -1 through 1.</param>
    /// <returns>A task completed after hover, wheel routing, and rendering settle.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A delta is outside the unit range.</exception>
    /// <exception cref="ArgumentException">The control is foreign, or both or neither wheel axes are requested.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="control"/> has empty arranged bounds.</exception>
    internal async Task WheelAsync(
        Control control,
        Point relative,
        int wheelX = 0,
        int wheelY = 0)
    {
        if (wheelX is < -1 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(wheelX), wheelX, "A wheel report carries one unit delta.");
        }

        if (wheelY is < -1 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(wheelY), wheelY, "A wheel report carries one unit delta.");
        }

        if (wheelX == 0 == (wheelY == 0))
        {
            throw new ArgumentException("Exactly one wheel axis must carry a unit delta.");
        }

        await MoveToAsync(control, relative);
        var button = wheelY == 1 ? 64 : wheelY == -1 ? 65 : wheelX == -1 ? 66 : 67;
        var point = _lastPoint!.Value;
        await _surface.SendAsync(Encode(button, point, final: 'M'), $"wheel pointer at {point}");
    }

    /// <summary>Moves a held primary pointer to one control-relative cell.</summary>
    /// <param name="control">The mounted control or one of its owned descendants.</param>
    /// <param name="relative">The zero-based cell inside the control.</param>
    /// <returns>A task completed after captured motion and rendering settle.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="control"/> is not owned by the surface.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="relative"/> lies outside the control.</exception>
    /// <exception cref="InvalidOperationException">The primary button is not pressed or the control is empty.</exception>
    internal async Task MovePressedToAsync(Control control, Point relative)
    {
        if (!_primaryPressed)
        {
            throw new InvalidOperationException("Press the primary pointer button before moving it as held.");
        }

        var point = await _surface.ResolvePointAsync(control, relative);
        await _surface.SendAsync(
            Encode(button: 32 + ModifierBits(_primaryModifiers), point, final: 'M'),
            $"drag primary pointer to {point}");
        _lastPoint = point;
    }

    /// <summary>Performs a complete captured primary drag between two control-relative cells.</summary>
    /// <param name="control">The mounted control or one of its owned descendants.</param>
    /// <param name="start">The zero-based starting cell inside the control.</param>
    /// <param name="end">The zero-based ending cell inside the control.</param>
    /// <returns>A task completed after move, press, held motion, release, and rendering settle.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="control"/> is not owned by the surface.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A relative endpoint lies outside the control.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="control"/> is empty.</exception>
    internal async Task DragAsync(Control control, Point start, Point end)
    {
        await MoveToAsync(control, start);
        await PressAsync();
        await MovePressedToAsync(control, end);
        await ReleaseAsync();
    }

    private static byte[] Encode(int button, Point point, char final) =>
        Encoding.ASCII.GetBytes(
            FormattableString.Invariant($"\u001b[<{button};{point.X + 1};{point.Y + 1}{final}"));

    private static int ModifierBits(Modifiers modifiers)
    {
        const Modifiers known = Modifiers.Shift | Modifiers.Alt | Modifiers.Control |
            Modifiers.Super | Modifiers.Hyper | Modifiers.Meta | Modifiers.CapsLock | Modifiers.NumLock;

        if ((modifiers & ~known) != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(modifiers), modifiers, "The pointer modifiers are undefined.");
        }

        const Modifiers supported = Modifiers.Shift | Modifiers.Alt | Modifiers.Control;

        return (modifiers & ~supported) != 0
            ? throw new NotSupportedException($"Component pointer encoding for {modifiers} is not supported.")
            : ((modifiers & Modifiers.Shift) != 0 ? 4 : 0) |
                ((modifiers & Modifiers.Alt) != 0 ? 8 : 0) |
                ((modifiers & Modifiers.Control) != 0 ? 16 : 0);
    }
}
