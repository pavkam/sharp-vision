// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Defines a focusable single-content control with reusable press interaction.</summary>
public abstract class Pressable: ContentControl
{
    private readonly PressInteraction _interaction;

    /// <summary>Initializes an empty focusable single-content control.</summary>
    protected Pressable()
    {
        _interaction = new PressInteraction(
            () => Bounds,
            () => EffectiveIsEnabled && EffectiveIsVisible,
            () => FocusOwner is null || IsFocused,
            RequestFocus,
            CapturePointer,
            () => HasPointerCapture,
            ReleasePointerCapture,
            SetPressed,
            Activate);
        CanFocus = true;
    }

    /// <summary>Completes one validated activation in a concrete control.</summary>
    /// <param name="cause">The input path that completed activation.</param>
    protected abstract void Activate(ActivationCause cause);

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        base.OnEvent(eventArgs);
        _interaction.Handle(eventArgs);
    }

    /// <inheritdoc/>
    protected override void OnFocusChanged(bool focused)
    {
        base.OnFocusChanged(focused);
        _interaction.FocusChanged(focused);
    }

    /// <inheritdoc/>
    protected override void OnPointerCaptureCancelled(ReleaseReason reason)
    {
        base.OnPointerCaptureCancelled(reason);
        _interaction.CaptureCancelled();
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);
        _interaction.Unavailable();
    }
}
