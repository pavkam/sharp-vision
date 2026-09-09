// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Composes one pointer/keyboard press-activation state machine that a control enables
/// through either a single whole-control target (<see cref="PressBehavior"/>) or several
/// owner-rendered sub-targets (<see cref="TargetedPressBehavior{TTarget}"/>), so the owning
/// <c>ControlBase</c> can route events and lifecycle notifications to whichever shape is active
/// without knowing which one it is.</summary>
internal interface IPressActivationBehavior: IControlLifecycleParticipant
{
    /// <summary>Routes one event through the press-activation state machine.</summary>
    /// <param name="eventArgs">The event to evaluate.</param>
    public void Handle(RoutedEventArgs eventArgs);

    /// <summary>Cancels any held press-activation pointer or keyboard state because the owning
    /// control became unavailable, without releasing pointer capture.</summary>
    public void Unavailable();

    /// <summary>Gets the currently pressed sub-target resolved by
    /// <see cref="TargetedPressBehavior{TTarget}"/>, boxed; always null for the single
    /// whole-control <see cref="PressBehavior"/>, and null whenever no press is currently
    /// held.</summary>
    public object? PressedTarget { get; }
}
