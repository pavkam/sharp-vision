// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;


/// <summary>Provides a concrete parent for shared control infrastructure tests.</summary>
internal sealed class ProbeContainer: Container
{
    /// <summary>Initializes a probe with an optional child capacity.</summary>
    /// <param name="capacity">The non-negative maximum child count.</param>
    internal ProbeContainer(int capacity = int.MaxValue) : base(capacity)
    {
    }

    /// <summary>Gets or sets whether rendering clips owned descendants.</summary>
    internal bool ClipChildren { get; set; } = true;

    /// <inheritdoc/>
    internal override bool ClipsChildren => ClipChildren;

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
