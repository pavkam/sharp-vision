// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Consumer.Tests;

/// <summary>Provides an external container whose child intentionally renders beyond its arranged box.</summary>
public sealed class OverflowPanel: Container
{
    /// <summary>Initializes an unclipped single-child panel.</summary>
    public OverflowPanel() : base(capacity: 1)
    {
    }

    /// <inheritdoc/>
    protected override bool ClipsChildren => false;

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint) =>
        Children.Count == 0 ? default : MeasureChild(Children[0], constraint);

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        if (Children.Count != 0)
        {
            ArrangeChild(
                Children[0],
                new Rect(bounds.Right, bounds.Y, 1, Math.Min(1, bounds.Height)),
                ResolvedAxes.Both);
        }
    }
}
