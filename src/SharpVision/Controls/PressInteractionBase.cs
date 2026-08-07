// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>
/// Defines a focusable control that participates in the shared pointer-press and Enter/Space
/// keyboard-activation interaction, independent of whatever content the concrete control owns.
/// </summary>
/// <remarks>
/// <see cref="PressableBase"/> layers a single owned text caption on top of this for controls
/// whose entire content is one label (a Button, a CheckBox). A control whose owned content is
/// richer than one caption - a drop-down field with a popup, for example - derives from this base
/// directly instead, since a single caption slot does not fit its composition.
/// </remarks>
[PublicAPI]
public abstract class PressInteractionBase: ControlBase
{
    /// <summary>Initializes the shared press interaction and makes the control focusable.</summary>
    protected PressInteractionBase()
    {
        Interaction = new PressBehavior(
            () => InteractionBounds,
            () => EffectiveIsEnabled && EffectiveIsVisible,
            () => FocusOwner is null || Focused,
            RequestFocus,
            CapturePointer,
            () => HasPointerCapture,
            ReleasePointerCapture,
            SetPressed,
            Activate,
            () => Capabilities.KeyReleaseEvents.Authoritative);
        Focusable = true;
        TabStop = true;
    }

    /// <summary>Gets the rectangle press interaction (pointer press/drag/release and hit testing
    /// via <see cref="ControlBase.HitTest"/>) is evaluated against. Defaults to <see cref="ControlBase.Bounds"/>.</summary>
    /// <remarks>
    /// A concrete control whose pressed visual state translates the drawn face away from
    /// <see cref="ControlBase.Bounds"/> - a Button showing a whole-cell shadow, for example -
    /// must override this to the same translated rectangle it actually paints, so the pointer
    /// geometry a user presses and releases against agrees with what is on screen.
    /// </remarks>
    protected virtual Rect InteractionBounds => Bounds;

    /// <summary>
    /// Gets the shared press-interaction state machine. A concrete control overrides
    /// <see cref="ControlBase.OnEvent"/> and calls <see cref="PressBehavior.Handle"/> on this
    /// instance at whatever point fits its own routing - deliberately not wired in automatically
    /// here, since a control with content richer than a single caption (a drop-down's own
    /// Escape/arrow-key handling, for example) commonly needs to intercept or short-circuit a
    /// routed event before press interaction ever sees it.
    /// </summary>
    /// <remarks>
    /// <see cref="PressBehavior"/> is internal, so this member cannot be <see langword="protected"/>
    /// without leaking an inaccessible type through a public base class; every concrete control
    /// that needs it is defined in this same assembly.
    /// </remarks>
    private protected PressBehavior Interaction { get; }

    /// <summary>Completes one validated activation in a concrete control.</summary>
    /// <param name="cause">The input path that completed activation.</param>
    protected abstract void Activate(ActivationCause cause);

    /// <inheritdoc/>
    protected override void OnFocusChanged(bool focused)
    {
        base.OnFocusChanged(focused);
        Interaction.FocusChanged(focused);
    }

    /// <inheritdoc/>
    protected override void OnLostPointerCapture(PointerCaptureLossReason reason)
    {
        base.OnLostPointerCapture(reason);
        Interaction.CaptureLost();
    }

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        base.OnUnavailable(reason);
        Interaction.Unavailable();
    }
}
