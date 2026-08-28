// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

using NonNegativeValue = JetBrains.Annotations.NonNegativeValueAttribute;

/// <summary>Arranges owned children sequentially on one terminal-cell axis.</summary>
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Stack is the approved concise terminal control name, not a collection type.")]
[PublicAPI]
public sealed class Stack: Container
{
    /// <summary>Initializes a stack that fills its parent cross-axis slot.</summary>
    public Stack()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        EnableChromeAuthoring();
    }

    /// <summary>Gets or sets the sequential layout axis.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public Orientation Orientation
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNotDefined(value, nameof(value), "The orientation is unknown.");

            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    } = Orientation.Vertical;

    /// <summary>Gets or sets non-negative cells between non-collapsed children.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    [NonNegativeValue]
    public int Spacing
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    }

    /// <summary>Gets or sets whether visual and default navigation order is reversed.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool Reverse
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.Arrange);
    }

    /// <inheritdoc/>
    internal override ControlBase NavigationAt(int index) =>
        Reverse && index < Children.Count ? Children[Children.Count - index - 1] : base.NavigationAt(index);

    /// <inheritdoc/>
    internal override bool AddSelectableTextChildren(List<ControlBase> children)
    {
        ArgumentNullException.ThrowIfNull(children);

        if (!Reverse)
        {
            return base.AddSelectableTextChildren(children);
        }

        for (var index = Children.Count - 1; index >= 0; index--)
        {
            children.Add(Children[index]);
        }

        return true;
    }

    /// <inheritdoc/>
    internal override ControlBase? HitTestPopupCore(Point point)
    {
        if (!Reverse)
        {
            return base.HitTestPopupCore(point);
        }

        for (var index = 0; index < Children.Count; index++)
        {
            if (Children[index].HitTestPopupBranch(point, OwnedControlLayer.Normal) is { } popup)
            {
                return popup;
            }
        }

        return null;
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var axis = 0;
        var cross = 0;
        var count = 0;

        foreach (var child in Children)
        {
            child.Measure(Orientation == Orientation.Vertical
                ? new Constraint(constraint.Width, height: null)
                : new Constraint(width: null, constraint.Height));

            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            var desiredAxis = Orientation == Orientation.Vertical
                ? child.DesiredSize.Height.Add(child.Margin.Vertical)
                : child.DesiredSize.Width.Add(child.Margin.Horizontal);
            var desiredCross = Orientation == Orientation.Vertical
                ? child.DesiredSize.Width.Add(child.Margin.Horizontal)
                : child.DesiredSize.Height.Add(child.Margin.Vertical);
            axis = axis.Add(desiredAxis);
            cross = Math.Max(cross, desiredCross);
            count++;
        }

        axis = axis.Add(SpacingExtent(count, int.MaxValue));
        return Orientation == Orientation.Vertical
            ? new Size(cross, axis)
            : new Size(axis, cross);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        var count = CountParticipants();

        if (count == 0)
        {
            return;
        }

        var rentedChildren = ArrayPool<ControlBase>.Shared.Rent(count);
        var rentedLengths = ArrayPool<Length>.Shared.Rent(count);
        var rentedAutomatic = ArrayPool<int>.Shared.Rent(count);
        var rentedMinimum = ArrayPool<int>.Shared.Rent(count);
        var rentedMaximum = ArrayPool<int>.Shared.Rent(count);
        var rentedExtents = ArrayPool<int>.Shared.Rent(count);
        var children = rentedChildren.AsSpan(0, count);
        var lengths = rentedLengths.AsSpan(0, count);
        var automatic = rentedAutomatic.AsSpan(0, count);
        var minimum = rentedMinimum.AsSpan(0, count);
        var maximum = rentedMaximum.AsSpan(0, count);
        var extents = rentedExtents.AsSpan(0, count);

        try
        {
            Fill(children, lengths, automatic, minimum, maximum);
            var vertical = Orientation == Orientation.Vertical;
            var axis = vertical ? bounds.Height : bounds.Width;
            var spacing = SpacingExtent(count, axis);
            var margins = SumMargins(children);

            // A scrolling stacking axis has no real ceiling - Container.ResolveContentSlot sizes
            // bounds to Math.Max(Extent, Viewport) specifically so overflowing content can lay
            // out past the visible area, and scrolling absorbs the rest. But Extent is, by
            // construction, the sum of every child's own pre-arrange intrinsic request, so a
            // Percent child's true (viewport-relative) size is never part of it - resolving
            // Percent against that stale total as a hard ceiling always produces an artificial
            // deficit that crushes it back to its own tiny intrinsic size, or to zero, the moment
            // an Auto sibling's own request already consumes most of the extent. An
            // unbounded pool removes that false ceiling entirely: every track gets its own full,
            // non-competing request, with Percent still sized against the visible viewport
            // instead of falling back to its own unrelated intrinsic request.
            var scrolls = AutoScroll &&
                (ScrollBars & (vertical ? ScrollBars.Vertical : ScrollBars.Horizontal)) != 0;
            int? available = scrolls ? null : Math.Max(0, axis - spacing - margins);
            int? percentBase = scrolls ? (vertical ? Viewport.Height : Viewport.Width) : axis;

            // Percentages otherwise resolve against the complete final content axis, not the
            // smaller area left after reserving spacing and margins - so the percentage base is
            // passed through explicitly instead of pre-converting Percent lengths to Cells, which
            // would hide them from Percent's shrink priority and lose their shared cumulative
            // rounding edges.
            Tracks.Resolve(available, lengths, automatic, minimum, maximum, extents, percentBase);
            Arrange(children, extents, bounds, spacing);
        }
        finally
        {
            children.Clear();
            lengths.Clear();
            automatic.Clear();
            minimum.Clear();
            maximum.Clear();
            extents.Clear();
            ArrayPool<ControlBase>.Shared.Return(rentedChildren, clearArray: true);
            ArrayPool<Length>.Shared.Return(rentedLengths);
            ArrayPool<int>.Shared.Return(rentedAutomatic);
            ArrayPool<int>.Shared.Return(rentedMinimum);
            ArrayPool<int>.Shared.Return(rentedMaximum);
            ArrayPool<int>.Shared.Return(rentedExtents);
        }
    }

    /// <inheritdoc/>
    internal override void RenderContent(TerminalCanvas canvas, Rect contentClip)
    {
        if (!Reverse)
        {
            base.RenderContent(canvas, contentClip);
            return;
        }

        for (var index = Children.Count - 1; index >= 0; index--)
        {
            if (Children[index].RendersInNormalLayer)
            {
                Children[index].Render(canvas, contentClip);
            }
        }
    }

    /// <inheritdoc/>
    internal override void RenderOwnedPopupDescendants(TerminalCanvas canvas)
    {
        if (!Reverse)
        {
            base.RenderOwnedPopupDescendants(canvas);
            return;
        }

        for (var index = Children.Count - 1; index >= 0; index--)
        {
            Children[index].RenderPopupBranch(canvas, OwnedControlLayer.Normal);
        }
    }

    [Pure]
    private int CountParticipants()
    {
        var count = 0;

        foreach (var child in Children)
        {
            if (child.Visibility != Visibility.Collapsed)
            {
                count++;
            }
        }

        return count;
    }

    private void Fill(
        Span<ControlBase> children,
        Span<Length> lengths,
        Span<int> automatic,
        Span<int> minimum,
        Span<int> maximum)
    {
        var position = 0;

        for (var offset = 0; offset < Children.Count; offset++)
        {
            var index = Reverse ? Children.Count - offset - 1 : offset;
            var child = Children[index];

            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            children[position] = child;
            lengths[position] = Orientation == Orientation.Vertical ? child.Height : child.Width;
            automatic[position] = Orientation == Orientation.Vertical
                ? child.DesiredSize.Height
                : child.DesiredSize.Width;
            minimum[position] = Orientation == Orientation.Vertical ? child.MinHeight : child.MinWidth;
            maximum[position] = Orientation == Orientation.Vertical ? child.MaxHeight : child.MaxWidth;
            position++;
        }

        Debug.Assert(position == children.Length, "Every participating child must have one track.");
    }

    [Pure]
    private int SumMargins(ReadOnlySpan<ControlBase> children)
    {
        Debug.Assert(children.Length >= 0, "Stack margin sum requires a valid span.");

        var result = 0;

        foreach (var child in children)
        {
            result = result.Add(Orientation == Orientation.Vertical
                    ? child.Margin.Vertical
                    : child.Margin.Horizontal);
        }

        return result;
    }

    private void Arrange(
        ReadOnlySpan<ControlBase> children,
        ReadOnlySpan<int> extents,
        Rect bounds,
        int spacing)
    {
        Debug.Assert(children.Length == extents.Length, "Every arranged child must have one extent.");
        Debug.Assert(spacing >= 0, "Stack spacing is non-negative.");

        var origin = Orientation == Orientation.Vertical ? bounds.Y : bounds.X;
        var remainingSpacing = spacing;

        for (var index = 0; index < children.Length; index++)
        {
            var child = children[index];
            var margin = Orientation == Orientation.Vertical
                ? child.Margin.Vertical
                : child.Margin.Horizontal;
            var outer = extents[index].Add(margin);
            var slot = Orientation == Orientation.Vertical
                ? new Rect(bounds.X, origin, bounds.Width, outer)
                : new Rect(origin, bounds.Y, outer, bounds.Height);

            child.Arrange(
                slot,
                widthResolved: Orientation == Orientation.Horizontal,
                heightResolved: Orientation == Orientation.Vertical);
            origin = origin.Add(outer);

            if (index < children.Length - 1)
            {
                var gap = Math.Min(Spacing, remainingSpacing);
                origin = origin.Add(gap);
                remainingSpacing -= gap;
            }
        }
    }

    [Pure]
    private int SpacingExtent(int count, int limit)
    {
        Debug.Assert(count >= 0, "Participant count is non-negative.");
        Debug.Assert(limit >= 0, "Spacing limit is non-negative.");

        if (count <= 1)
        {
            return 0;
        }

        var requested = (long) Spacing * (count - 1);
        return (int) Math.Min(limit, Math.Min(int.MaxValue, requested));
    }
}
