// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Popups;

/// <summary>Displays anchored popup content with automatic light dismiss and sibling-flyout exclusion.</summary>
[PublicAPI]
public sealed class Flyout: Popup
{
    /// <summary>Initializes a closed flyout with direct popup presentation and light-dismiss behavior.</summary>
    public Flyout()
    {
        ModalBehavior = PopupModalBehavior.None;
        SuppressCloseOtherPopups = true;
        CloseOnEscape = true;
        FocusOnOpen = true;
        ConfigureLightDismiss(new PopupLightDismissPolicy(
            includeAnchor: true,
            buttons: Terminal.Input.Buttons.Primary,
            interceptAtModalBoundary: false,
            dismiss: () => IsOpen = false));
    }

    /// <summary>Sets the anchor and opens the flyout in one call.</summary>
    /// <param name="anchor">The non-null anchor control.</param>
    /// <exception cref="ArgumentNullException"><paramref name="anchor"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The attached flyout is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The flyout is disposed.</exception>
    public void ShowAt(ControlBase anchor)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        Anchor = anchor;
        IsOpen = true;
    }

    /// <summary>Follows the anchor reflowing (a sibling growing above it, its own container
    /// resizing it, and so on) by dismissing rather than repositioning — a Flyout's outside-press
    /// light dismiss already assumes its bounds are fixed for the pointer geometry captured when
    /// it opened, so a moved anchor closes it instead of chasing the new position.</summary>
    /// <inheritdoc/>
    internal override void OnAnchorReflow() => IsOpen = false;

    /// <inheritdoc/>
    internal override bool OnContentAvailable() => ExcludePopupPeers(
        static candidate => candidate is Flyout,
        candidate => IsAncestorOf(candidate, this),
        base.OnContentAvailable);
}
