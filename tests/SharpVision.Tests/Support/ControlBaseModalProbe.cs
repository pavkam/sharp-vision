// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Composes <see cref="ControlBase.EnterOwnedModal"/> directly, without deriving
/// <see cref="Menu"/>, <see cref="PopupModalTracker"/>, or
/// <see cref="SharpVision.Surfaces.FloatingSurfaceBase"/>, to prove the capability is reachable from any
/// control that owns a <see cref="ModalSession"/>.</summary>
internal sealed class ControlBaseModalProbe: ControlBase
{
    private readonly ModalSession _session = new();

    /// <summary>Initializes a stretching, focusable probe that enters a modal session on
    /// activation.</summary>
    internal ControlBaseModalProbe()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        IsFocusable = true;
        IsTabStop = true;
        EnablePressActivation();
    }

    /// <summary>Gets the scope entered by the most recent activation, or null before any.</summary>
    internal ModalScope? Scope { get; private set; }

    /// <summary>Gets whether the tracked session still owns an active scope.</summary>
    internal bool IsModalSessionActive => _session.IsActive;

    /// <inheritdoc/>
    protected override void Activate(ActivationCause cause)
    {
        _ = cause;
        Scope = EnterOwnedModal(_session, OutsideInteraction.Ignore, initialFocus: null);
    }

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
        HandlePressActivation(eventArgs);
    }
}
