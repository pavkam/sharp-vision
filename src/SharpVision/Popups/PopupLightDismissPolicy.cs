// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Popups;

using SharpVision.Terminal.Input;

/// <summary>Describes one Popup-owned light-dismiss registration.</summary>
internal sealed class PopupLightDismissPolicy
{
    /// <summary>Initializes one validated light-dismiss policy.</summary>
    /// <param name="includeAnchor">Whether the current Popup anchor is inside the dismissal surface.</param>
    /// <param name="buttons">The non-empty set of pointer buttons that dismisses the Popup.</param>
    /// <param name="interceptAtModalBoundary">Whether dismissal participates at a modal boundary.</param>
    /// <param name="dismiss">The callback that requests family-specific closure.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="buttons"/> is empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="dismiss"/> is null.</exception>
    internal PopupLightDismissPolicy(
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
    internal bool IncludeAnchor { get; }

    /// <summary>Gets the pointer buttons that dismiss the Popup.</summary>
    internal Buttons Buttons { get; }

    /// <summary>Gets whether dismissal participates at a modal boundary.</summary>
    internal bool InterceptAtModalBoundary { get; }

    /// <summary>Gets the callback that requests family-specific closure.</summary>
    internal Action Dismiss { get; }
}
