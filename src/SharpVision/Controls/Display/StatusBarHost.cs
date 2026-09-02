// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Display;

/// <summary>Arranges status items into stable leading and trailing edge groups.</summary>
internal sealed class StatusBarHost: Container
{
    /// <summary>Initializes a host that consumes the status bar's complete horizontal slot.</summary>
    public StatusBarHost()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        Face = ControlStyle.Default.Face;
    }

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

            width = width.Add(desired.Width.Add(child.Margin.Horizontal));
            height = Math.Max(height, desired.Height.Add(child.Margin.Vertical));
            count++;
        }

        width = width.Add(LayoutMath.GapExtent(Spacing, count, int.MaxValue));
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
            var origin = bounds.X.Add(remaining - extent);

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
                leftOrigin = leftOrigin.Add(gap);
                remaining -= gap;
            }

            var extent = Math.Min(OuterWidth(item), remaining);

            ArrangeChild(item, new Rect(leftOrigin, bounds.Y, extent, bounds.Height), ResolvedAxes.Width);

            leftOrigin = leftOrigin.Add(extent);
            remaining -= extent;
            leftArranged++;
        }
    }

    [Pure]
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

    [Pure]
    private static int OuterWidth(ControlBase item) => item.DesiredSize.Width.Add(item.Margin.Horizontal);

}
