// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;


/// <summary>A container probe that measures the union of its children and arranges each child to its slot.</summary>
internal sealed class LayoutProbe: Container
{
    /// <summary>Initializes a probe with an optional child capacity.</summary>
    /// <param name="capacity">The non-negative maximum child count.</param>
    internal LayoutProbe(int capacity = int.MaxValue) : base(capacity)
    {
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        int width = 0;
        int height = 0;

        foreach (Control child in Children)
        {
            child.Measure(constraint);
            width = Math.Max(width, child.DesiredSize.Width);
            height = Math.Max(height, child.DesiredSize.Height);
        }

        return new Size(width, height);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        foreach (Control child in Children)
        {
            child.Arrange(bounds);
        }
    }
}
