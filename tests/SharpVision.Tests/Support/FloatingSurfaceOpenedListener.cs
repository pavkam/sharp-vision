// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Provides a collectible target for floating-surface lifecycle subscriptions.</summary>
internal sealed class FloatingSurfaceOpenedListener
{
    /// <summary>Receives an Opened notification without retaining other state.</summary>
    internal void Handle(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
    }
}
