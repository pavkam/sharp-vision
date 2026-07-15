// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Consumer.Tests;

/// <summary>Provides an externally authored horizontal multi-child layout container.</summary>
public sealed class FlowPanel: Container
{
    /// <summary>Initializes an empty horizontal flow panel.</summary>
    public FlowPanel()
    {
    }

    /// <summary>Gets or sets whether owned children are clipped to the panel bounds.</summary>
    /// <exception cref="InvalidOperationException">The attached panel is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The panel is disposed.</exception>
    public bool ClipChildren
    {
        get;
        set => _ = SetProperty(ref field, value, ChangeImpact.Render);
    } = true;

    /// <inheritdoc/>
    protected override bool ClipsChildren => ClipChildren;

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var width = 0;
        var height = 0;

        foreach (var child in Children)
        {
            var desired = MeasureChild(child, constraint);
            width = SaturatingAdd(width, desired.Width);
            height = Math.Max(height, desired.Height);
        }

        return new Size(width, height);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        var x = bounds.X;

        foreach (var child in Children)
        {
            var remaining = Math.Max(0, bounds.Right - x);
            var width = Math.Min(child.DesiredSize.Width, remaining);
            ArrangeChild(
                child,
                new Rect(x, bounds.Y, width, bounds.Height),
                ResolvedAxes.Both);
            x = SaturatingAdd(x, width);
        }
    }

    private static int SaturatingAdd(int left, int right)
    {
        var result = (long) left + right;
        return result > int.MaxValue ? int.MaxValue : (int) result;
    }
}
