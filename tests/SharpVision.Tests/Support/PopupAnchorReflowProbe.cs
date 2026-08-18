// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Observes the Popup anchor-reflow response seam.</summary>
internal sealed class PopupAnchorReflowProbe: Popup
{
    /// <summary>Gets the number of anchor-reflow calls.</summary>
    internal int AnchorReflowCalls { get; private set; }

    /// <inheritdoc/>
    internal override void OnAnchorReflow()
    {
        AnchorReflowCalls++;
        base.OnAnchorReflow();
    }
}
