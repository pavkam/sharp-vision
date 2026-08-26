// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision;

using SharpVision.Terminal.Input;

/// <summary>Exposes the last observed pointer state and current pointer targets.</summary>
/// <remarks>
/// This is a pull-style snapshot updated on the dispatcher as pointer input is
/// dispatched. It never throws; positions are null before the first pointer and
/// after a leave. Targets read through the capture manager once the tree attaches.
/// </remarks>
[PublicAPI]
public sealed class PointerDevice
{
    private readonly Func<PointerManager?> _capture;

    internal PointerDevice(Func<PointerManager?> capture)
    {
        Debug.Assert(capture is not null, "The capture accessor must be provided.");
        _capture = capture;
    }

    /// <summary>Gets the last observed zero-based cell position, or null.</summary>
    public Point? Position { get; private set; }

    /// <summary>Gets the last observed zero-based pixel position, or null.</summary>
    public Point? PixelPosition { get; private set; }

    /// <summary>Gets the accumulated physical buttons held as of the last pointer.</summary>
    public Buttons Buttons { get; private set; }

    /// <summary>Gets the modifiers active as of the last pointer.</summary>
    public Modifiers Modifiers { get; private set; }

    /// <summary>Gets the action of the last pointer.</summary>
    public PointerAction LastAction { get; private set; }

    /// <summary>Gets the current interactive hover target, or null when the pointer is over non-interactive content.</summary>
    public ControlBase? Hovered => _capture()?.Hovered;

    /// <summary>Gets the origin of the oldest surviving raw pointer press, or null.</summary>
    public ControlBase? PressOrigin => _capture()?.PressOrigin;

    /// <summary>Gets the exclusive capture target, or null.</summary>
    public ControlBase? Captured => _capture()?.Captured;

    internal void Observe(in Pointer pointer)
    {
        Buttons = pointer.Action switch
        {
            PointerAction.Press => Buttons | pointer.Buttons,
            PointerAction.Release when pointer.Buttons == Buttons.None => Buttons.None,
            PointerAction.Release => Buttons & ~pointer.Buttons,
            PointerAction.Move when pointer.Buttons == Buttons.None => Buttons.None,
            PointerAction.Move => Buttons | pointer.Buttons,
            PointerAction.Leave => Buttons.None,
            PointerAction.Wheel => Buttons,
            _ => throw new UnreachableException()
        };
        Modifiers = pointer.Modifiers;
        LastAction = pointer.Action;

        if (pointer.Action == PointerAction.Leave)
        {
            Position = null;
            PixelPosition = null;
            return;
        }

        Position = pointer.Cells;
        PixelPosition = pointer.Pixels;
    }
}
