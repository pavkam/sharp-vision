// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Runtime;

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

    /// <summary>Gets the buttons held as of the last pointer.</summary>
    public Buttons Buttons { get; private set; }

    /// <summary>Gets the modifiers active as of the last pointer.</summary>
    public Modifiers Modifiers { get; private set; }

    /// <summary>Gets the action of the last pointer.</summary>
    public PointerAction LastAction { get; private set; }

    /// <summary>Gets the current interactive hover target, or null when the pointer is over non-interactive content.</summary>
    public Control? Hovered => _capture()?.Hovered;

    /// <summary>Gets the control where the raw active pointer press began, or null.</summary>
    public Control? PressOrigin => _capture()?.PressOrigin;

    /// <summary>Gets the exclusive capture target, or null.</summary>
    public Control? Captured => _capture()?.Captured;

    internal void Observe(in Pointer pointer)
    {
        Buttons = pointer.Buttons;
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
