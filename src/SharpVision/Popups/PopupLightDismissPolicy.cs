// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Popups;

using SharpVision.Terminal.Input;

/// <summary>Describes one Popup-owned light-dismiss registration.</summary>
/// <remarks>
/// An externally defined Popup family passes one of these to
/// <see cref="Popup.ConfigureLightDismiss(PopupLightDismissPolicy)"/>, exactly once, before the
/// Popup is attached or opened, to opt into the shared outside-press dismissal that Flyout and
/// ContextMenu also use. This is a plain value object: nothing about it changes after
/// construction, and a family that needs different dismissal behavior later constructs and
/// configures a new Popup instance rather than mutating an existing policy.
/// </remarks>
[PublicAPI]
public sealed class PopupLightDismissPolicy
{
    /// <summary>Initializes one validated light-dismiss policy.</summary>
    /// <param name="includeAnchor">Whether the current Popup anchor is inside the dismissal surface.</param>
    /// <param name="buttons">The non-empty set of pointer buttons that dismisses the Popup.</param>
    /// <param name="interceptAtModalBoundary">Whether dismissal participates at a modal boundary.</param>
    /// <param name="dismiss">The callback that requests family-specific closure.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="buttons"/> is empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="dismiss"/> is null.</exception>
    public PopupLightDismissPolicy(
        bool includeAnchor,
        Buttons buttons,
        bool interceptAtModalBoundary,
        Action dismiss)
    {
        if (buttons == Buttons.None)
        {
            throw new ArgumentOutOfRangeException(nameof(buttons), buttons, "Light dismiss requires at least one button.");
        }

        ArgumentNullException.ThrowIfNull(dismiss);
        IncludeAnchor = includeAnchor;
        Buttons = buttons;
        InterceptAtModalBoundary = interceptAtModalBoundary;
        Dismiss = dismiss;
    }

    /// <summary>Gets whether the current Popup anchor is inside the dismissal surface.</summary>
    /// <remarks>
    /// A Flyout sets this true so a press on the control that opened it does not itself dismiss
    /// it; a Popup with no fixed anchor relationship to a single control typically sets it false.
    /// </remarks>
    public bool IncludeAnchor { get; }

    /// <summary>Gets the pointer buttons that dismiss the Popup.</summary>
    public Buttons Buttons { get; }

    /// <summary>Gets whether dismissal participates at a modal boundary.</summary>
    /// <remarks>
    /// True lets an outside press that lands on another surface's modal boundary still dismiss
    /// this Popup before that boundary handles the press itself, matching a ContextMenu's
    /// expectation that opening a second context menu first dismisses the one already open.
    /// </remarks>
    public bool InterceptAtModalBoundary { get; }

    /// <summary>Gets the callback that requests family-specific closure.</summary>
    /// <remarks>
    /// Invoked with no arguments when an outside press or modal-boundary interception (per
    /// <see cref="InterceptAtModalBoundary"/>) is classified as a dismissal; the callback owns
    /// deciding how its Popup actually closes.
    /// </remarks>
    public Action Dismiss { get; }
}
