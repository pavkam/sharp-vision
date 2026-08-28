// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Popup test double that mimics Flyout's dismiss-on-reflow policy and, once its own
/// anchor-reflow subscription is live, immediately forces the anchor to reflow again - simulating
/// a reflow landing while this popup's own opening transition is still on the call stack, the
/// scenario the shared IsOpen/_isOpenTransitioning reentrancy guard must survive.</summary>
internal sealed class PopupAnchorReflowReentrancyProbe: Popup
{
    /// <summary>Gets or sets the bounds forced onto Anchor immediately after subscribing, or null
    /// to leave the anchor alone.</summary>
    internal Rect? ReflowAnchorDuringOpenTo { get; set; }

    /// <inheritdoc/>
    internal override bool OnContentAvailable()
    {
        if (!base.OnContentAvailable())
        {
            return false;
        }

        if (ReflowAnchorDuringOpenTo is { } bounds && Anchor is { } anchor)
        {
            anchor.Arrange(bounds, widthResolved: true, heightResolved: true);
        }

        return true;
    }

    /// <inheritdoc/>
    internal override void OnAnchorReflow() => IsOpen = false;
}
