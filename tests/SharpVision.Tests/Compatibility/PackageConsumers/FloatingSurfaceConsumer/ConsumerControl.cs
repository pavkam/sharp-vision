// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.FloatingSurfaceConsumer;

/// <summary>Proves an external package consumer can inherit the generic typed control.</summary>
public sealed class ConsumerControl: Control<ConsumerSurfaceStyle>
{
    /// <summary>Initializes one externally defined typed control.</summary>
    public ConsumerControl() : base(ConsumerSurfaceStyle.Definition)
    {
    }

    /// <inheritdoc/>
    protected override void OnStyleChanged(ConsumerSurfaceStyle previous, ConsumerSurfaceStyle current)
    {
        _ = previous;
        Border = current.Border;
        Shadow = current.Shadow;
    }
}
