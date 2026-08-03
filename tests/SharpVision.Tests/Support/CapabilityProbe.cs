// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Records inherited terminal color-depth transitions for context tests.</summary>
internal sealed class CapabilityProbe: ControlBase
{
    /// <summary>Gets the currently inherited terminal color depth.</summary>
    internal ColorDepth ColorDepth => Capabilities.ColorDepth;

    /// <summary>Gets committed terminal color-depth transitions in publication order.</summary>
    internal List<ColorDepth> Transitions { get; } = [];

    /// <inheritdoc/>
    protected override void OnCapabilitiesChanged(
        Capabilities previous,
        Capabilities current)
    {
        if (previous.ColorDepth != current.ColorDepth)
        {
            Transitions.Add(current.ColorDepth);
        }
    }
}
