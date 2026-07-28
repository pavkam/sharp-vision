// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Display;

/// <summary>Arranges status items into stable leading and trailing edge groups.</summary>
internal sealed class StatusBarHost: Container
{
    /// <summary>Initializes a host that consumes the status bar's complete horizontal slot.</summary>
    public StatusBarHost() => HorizontalAlignment = HorizontalAlignment.Stretch;

    /// <summary>Gets or sets the non-negative cells between adjacent visible status items.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached host is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The host is disposed.</exception>
    public int Spacing
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    } = 1;

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var width = 0;
        var height = 0;
        var count = 0;

        foreach (var child in Children)
        {
            Debug.Assert(child is StatusBarItem, "The private status host contains only status items.");
            var desired = MeasureChild(child, new Constraint(width: null, constraint.Height));

            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            width = LayoutMath.Add(width, LayoutMath.Add(desired.Width, child.Margin.Horizontal));
            height = Math.Max(height, LayoutMath.Add(desired.Height, child.Margin.Vertical));
            count++;
        }

        width = LayoutMath.Add(width, SpacingExtent(count, int.MaxValue));
        return new Size(width, height);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        var leftCount = Count(StatusBarItemAlignment.Left);
        var rightCount = Count(StatusBarItemAlignment.Right);
        var remaining = bounds.Width;
        var rightArranged = 0;

        // Allocate from the physical trailing edge in reverse collection order.
        // The last status item therefore remains readable when the viewport is tiny.
        for (var index = Children.Count - 1; index >= 0; index--)
        {
            var item = (StatusBarItem) Children[index];

            if (item.Visibility == Visibility.Collapsed || item.Alignment != StatusBarItemAlignment.Right)
            {
                continue;
            }

            if (rightArranged > 0)
            {
                remaining -= Math.Min(Spacing, remaining);
            }

            var extent = Math.Min(OuterWidth(item), remaining);
            var origin = bounds.X + remaining - extent;

            ArrangeChild(item, new Rect(origin, bounds.Y, extent, bounds.Height), ResolvedAxes.Width);

            remaining -= extent;
            rightArranged++;
        }

        if (leftCount > 0 && rightCount > 0)
        {
            remaining -= Math.Min(Spacing, remaining);
        }

        var leftOrigin = bounds.X;
        var leftArranged = 0;

        foreach (var child in Children)
        {
            var item = (StatusBarItem) child;

            if (item.Visibility == Visibility.Collapsed || item.Alignment != StatusBarItemAlignment.Left)
            {
                continue;
            }

            if (leftArranged > 0)
            {
                var gap = Math.Min(Spacing, remaining);
                leftOrigin += gap;
                remaining -= gap;
            }

            var extent = Math.Min(OuterWidth(item), remaining);

            ArrangeChild(item, new Rect(leftOrigin, bounds.Y, extent, bounds.Height), ResolvedAxes.Width);

            leftOrigin += extent;
            remaining -= extent;
            leftArranged++;
        }
    }

    private int Count(StatusBarItemAlignment alignment)
    {
        var count = 0;

        foreach (var child in Children)
        {
            if (child is StatusBarItem item &&
                item.Visibility != Visibility.Collapsed &&
                item.Alignment == alignment)
            {
                count++;
            }
        }

        return count;
    }

    private static int OuterWidth(Control item) => LayoutMath.Add(item.DesiredSize.Width, item.Margin.Horizontal);

    private int SpacingExtent(int count, int limit)
    {
        Debug.Assert(count >= 0, "Status item count is non-negative.");
        Debug.Assert(limit >= 0, "Status item spacing limit is non-negative.");

        if (count <= 1)
        {
            return 0;
        }

        var requested = (long) Spacing * (count - 1);
        return (int) Math.Min(limit, Math.Min(int.MaxValue, requested));
    }
}
