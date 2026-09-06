// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Composes <see cref="ControlBase.EnableDrag"/> directly, without deriving
/// <see cref="InputBase"/>, to prove the capability is reachable from any control.</summary>
internal sealed class DragActivationProbe: ControlBase
{
    /// <summary>Initializes a stretching, focusable probe with drag enabled.</summary>
    internal DragActivationProbe()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        IsFocusable = true;
        IsTabStop = true;
        EnableDrag();
    }

    /// <summary>Gets whether a drag enabled through <see cref="ControlBase.EnableDrag"/> is
    /// currently in progress.</summary>
    internal bool IsDraggingNow => IsDragging;

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        return new Size(1, 1);
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        base.OnEvent(eventArgs);

        if (eventArgs is not PointerEventArgs pointerArgs)
        {
            return;
        }

        var pointer = pointerArgs.Pointer;

        if (IsDragging)
        {
            if (pointer.Action == PointerAction.Leave || PointerButtonTransition.IsPrimaryRelease(pointer))
            {
                CancelDrag(releaseCapture: true);
            }

            return;
        }

        if (pointer.Action == PointerAction.Press &&
            (pointer.Buttons & Buttons.Primary) != 0 &&
            pointer.Cells is { } cells &&
            ContentBounds.Contains(cells))
        {
            _ = TryStartDrag(cells);
        }
    }
}
