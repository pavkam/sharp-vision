// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Popups;

/// <summary>Displays anchored popup content with automatic light dismiss and sibling-flyout exclusion.</summary>
[PublicAPI]
public sealed class Flyout: Popup
{
    private LightDismiss? _lightDismiss;

    /// <summary>Initializes a closed flyout with direct popup presentation and light-dismiss behavior.</summary>
    public Flyout()
    {
        ModalBehavior = PopupModalBehavior.None;
        SuppressCloseOtherPopups = true;
        CloseOnEscape = true;
        FocusOnOpen = true;
        PropertyChanged += OnFlyoutPropertyChanged;
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

    /// <inheritdoc/>
    protected override void OnUnavailable(ReleaseReason reason)
    {
        if (reason == ReleaseReason.Disposed)
        {
            PropertyChanged -= OnFlyoutPropertyChanged;
            StopLightDismiss();
        }

        base.OnUnavailable(reason);
    }

    private void OnFlyoutPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        _ = sender;

        if (eventArgs.PropertyName == nameof(IsOpen))
        {
            if (IsOpen)
            {
                StartLightDismiss();
            }
            else
            {
                StopLightDismiss();
            }
        }
        else if (eventArgs.PropertyName == nameof(Anchor) && IsOpen)
        {
            StartLightDismiss();
        }
    }

    /// <summary>Follows the anchor reflowing (a sibling growing above it, its own container
    /// resizing it, and so on) by dismissing rather than repositioning — a Flyout's outside-press
    /// light dismiss already assumes its bounds are fixed for the pointer geometry captured when
    /// it opened, so a moved anchor closes it instead of chasing the new position.</summary>
    /// <inheritdoc/>
    internal override void OnAnchorReflow() => IsOpen = false;

    private void StartLightDismiss()
    {
        CloseOtherFlyouts();
        StopLightDismiss();
        _lightDismiss = new LightDismiss(
            this,
            Anchor,
            () => IsOpen,
            () => SurfaceBounds,
            () => IsOpen = false);
    }

    private void StopLightDismiss()
    {
        _lightDismiss?.Dispose();
        _lightDismiss = null;
    }

    private void CloseOtherFlyouts()
    {
        ControlBase root = this;

        while (root.Parent is { } parent)
        {
            root = parent;
        }

        CloseDescendantFlyouts(root, this);
    }

    private static void CloseDescendantFlyouts(ControlBase control, Flyout except)
    {
        var count = control.OwnedControlCount;

        for (var index = 0; index < count; index++)
        {
            var child = control.OwnedControlAt(index);

            if (child is Flyout { IsOpen: true } flyout && !ReferenceEquals(flyout, except))
            {
                flyout.IsOpen = false;
            }

            CloseDescendantFlyouts(child, except);
        }
    }
}
