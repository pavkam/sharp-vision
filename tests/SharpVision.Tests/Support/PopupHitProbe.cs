// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Counts elevated hit-test discovery and returns itself as the discovered target.</summary>
internal sealed class PopupHitProbe: ControlBase
{
    /// <summary>Gets the number of elevated discovery calls.</summary>
    internal int PopupHitTestCalls { get; private set; }

    /// <inheritdoc/>
    internal override ControlBase? HitTestPopupCore(Point point)
    {
        _ = point;
        PopupHitTestCalls++;
        return this;
    }
}
