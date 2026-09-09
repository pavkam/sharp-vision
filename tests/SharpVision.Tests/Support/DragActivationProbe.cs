// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Composes <see cref="ControlBase.EnableDrag"/> directly, without deriving
/// <see cref="InputBase"/>, to prove the capability is reachable from any control and to record
/// every <see cref="ControlBase.HandleDrag"/>-routed <see cref="ControlBase.OnDragMoved"/> and
/// <see cref="ControlBase.OnDragEnded"/> callback.</summary>
internal sealed class DragActivationProbe: ControlBase
{
    /// <summary>Initializes a stretching, focusable probe with drag enabled.</summary>
    internal DragActivationProbe()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        IsFocusable = true;
        IsTabStop = true;
        EnableDrag(isAvailable: () =>
            !IsDisposed && EffectiveIsEnabled && EffectiveIsVisible && IsAvailableOverride);
    }

    /// <summary>Gets or sets an additional availability condition folded into the drag's
    /// <c>isAvailable</c> predicate, so a test can drop availability mid-gesture without disabling
    /// or hiding the probe outright.</summary>
    internal bool IsAvailableOverride { get; set; } = true;

    /// <summary>Gets whether a drag enabled through <see cref="ControlBase.EnableDrag"/> is
    /// currently in progress.</summary>
    internal bool IsDraggingNow => IsDragging;

    /// <summary>Gets the pointer cell the active drag started at, through the protected
    /// <see cref="ControlBase.DragStart"/> seam.</summary>
    internal Point DragStartNow => DragStart;

    /// <summary>Gets every <see cref="DragMove"/> reported to <see cref="OnDragMoved"/>, in order.</summary>
    internal List<DragMove> DragMoves { get; } = [];

    /// <summary>Gets the number of completed <see cref="OnDragEnded"/> callbacks.</summary>
    internal int DragEndedCalls { get; private set; }

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

        if (HandleDrag(pointerArgs))
        {
            return;
        }

        var pointer = pointerArgs.Pointer;

        if (pointer.Action == PointerAction.Press &&
            (pointer.Buttons & Buttons.Primary) != 0 &&
            pointer.Cells is { } cells &&
            ContentBounds.Contains(cells))
        {
            _ = TryStartDrag(cells);
        }
    }

    /// <inheritdoc/>
    protected override void OnDragMoved(DragMove move) => DragMoves.Add(move);

    /// <inheritdoc/>
    protected override void OnDragEnded() => DragEndedCalls++;
}
