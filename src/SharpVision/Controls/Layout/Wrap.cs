// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

using NonNegativeValue = JetBrains.Annotations.NonNegativeValueAttribute;

/// <summary>Arranges owned children in successive lines along a terminal-cell axis.</summary>
[PublicAPI]
public sealed class Wrap: Container
{
    /// <summary>Initializes a wrap panel that fills its parent cross-axis slot.</summary>
    public Wrap()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        EnableChromeAuthoring();
    }

    /// <summary>Gets or sets the axis along which each line progresses.</summary>
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
    } = Orientation.Horizontal;

    /// <summary>Gets or sets non-negative cells between adjacent children in one line.</summary>
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

    /// <summary>Gets or sets non-negative cells between adjacent wrapped lines.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    [NonNegativeValue]
    public int LineSpacing
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var count = CountParticipants();

        if (count == 0)
        {
            return default;
        }

        var rentedChildren = ArrayPool<ControlBase>.Shared.Rent(count);
        var rentedOuterSizes = ArrayPool<Size>.Shared.Rent(count);
        var rentedOuterSlots = ArrayPool<Rect>.Shared.Rent(count);
        var children = rentedChildren.AsSpan(0, count);
        var outerSizes = rentedOuterSizes.AsSpan(0, count);
        var outerSlots = rentedOuterSlots.AsSpan(0, count);

        try
        {
            FillMeasuredParticipants(children, outerSizes, constraint);
            return WrapLayout.Pack(
                outerSizes,
                Primary(constraint),
                Orientation,
                Spacing,
                LineSpacing,
                outerSlots);
        }
        finally
        {
            children.Clear();
            outerSizes.Clear();
            outerSlots.Clear();
            ArrayPool<ControlBase>.Shared.Return(rentedChildren, clearArray: true);
            ArrayPool<Size>.Shared.Return(rentedOuterSizes);
            ArrayPool<Rect>.Shared.Return(rentedOuterSlots);
        }
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
        var rentedOuterSizes = ArrayPool<Size>.Shared.Rent(count);
        var rentedOuterSlots = ArrayPool<Rect>.Shared.Rent(count);
        var children = rentedChildren.AsSpan(0, count);
        var outerSizes = rentedOuterSizes.AsSpan(0, count);
        var outerSlots = rentedOuterSlots.AsSpan(0, count);
        var constraint = new Constraint(bounds.Width, bounds.Height);

        try
        {
            FillMeasuredParticipants(children, outerSizes, constraint);
            _ = WrapLayout.Pack(
                outerSizes,
                PrimaryPackingLimit(bounds),
                Orientation,
                Spacing,
                LineSpacing,
                outerSlots);

            for (var index = 0; index < count; index++)
            {
                var slot = outerSlots[index];
                var contained = ContainCrossAxis(slot, bounds);
                var resolvedAxes = Orientation == Orientation.Horizontal ? ResolvedAxes.Width : ResolvedAxes.Height;

                if (contained != slot)
                {
                    resolvedAxes |= Orientation == Orientation.Horizontal ? ResolvedAxes.Height : ResolvedAxes.Width;
                }

                ArrangeChild(
                    children[index],
                    new Rect(bounds.X.Add(contained.X), bounds.Y.Add(contained.Y), contained.Width, contained.Height),
                    resolvedAxes,
                    widthLimitBase: Orientation == Orientation.Horizontal
                        ? PrimaryLimitBase(bounds)
                        : null,
                    heightLimitBase: Orientation == Orientation.Vertical
                        ? PrimaryLimitBase(bounds)
                        : null);
            }
        }
        finally
        {
            children.Clear();
            outerSizes.Clear();
            outerSlots.Clear();
            ArrayPool<ControlBase>.Shared.Return(rentedChildren, clearArray: true);
            ArrayPool<Size>.Shared.Return(rentedOuterSizes);
            ArrayPool<Rect>.Shared.Return(rentedOuterSlots);
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

    private void FillMeasuredParticipants(
        Span<ControlBase> children,
        Span<Size> outerSizes,
        Constraint constraint)
    {
        Debug.Assert(children.Length == outerSizes.Length, "Every wrap participant has one outer size.");

        var index = 0;

        foreach (var child in Children)
        {
            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            children[index] = child;
            var desired = MeasureChild(child, ChildMeasureConstraint(child, constraint));
            desired = RemeasureViewportRelativeLimits(child, constraint, desired);
            outerSizes[index] = new Size(
                desired.Width.Add(child.Margin.Horizontal),
                desired.Height.Add(child.Margin.Vertical));
            index++;
        }

        Debug.Assert(index == children.Length, "Every non-collapsed child is a wrap participant.");
    }

    [Pure]
    private int? Primary(Constraint constraint) =>
        Orientation == Orientation.Horizontal ? constraint.Width : constraint.Height;

    [Pure]
    private int Primary(Rect bounds) =>
        Orientation == Orientation.Horizontal ? bounds.Width : bounds.Height;

    [Pure]
    private Constraint ChildMeasureConstraint(ControlBase child, Constraint constraint) =>
        !ScrollsPrimary()
            ? constraint
            : UsesViewportPrimaryLength(child)
                ? Orientation == Orientation.Horizontal
                    ? new Constraint(ScrollMeasureViewport.Width, constraint.Height)
                    : new Constraint(constraint.Width, ScrollMeasureViewport.Height)
                : Orientation == Orientation.Horizontal
                    ? new Constraint(width: null, constraint.Height)
                    : new Constraint(constraint.Width, height: null);

    [Pure]
    private int? PrimaryPackingLimit(Rect bounds) =>
        ScrollsPrimary() ? null : Primary(bounds);

    [Pure]
    private int? PrimaryLimitBase(Rect bounds) =>
        ScrollsPrimary()
            ? Orientation == Orientation.Horizontal ? Viewport.Width : Viewport.Height
            : Primary(bounds);

    [Pure]
    private Rect ContainCrossAxis(Rect slot, Rect bounds)
    {
        var crossLimit = Orientation == Orientation.Horizontal ? bounds.Height : bounds.Width;
        var crossOrigin = Orientation == Orientation.Horizontal ? slot.Y : slot.X;
        var crossExtent = Orientation == Orientation.Horizontal ? slot.Height : slot.Width;
        var containedOrigin = Math.Min(crossOrigin, crossLimit);
        var containedExtent = Math.Min(crossExtent, Math.Max(0, crossLimit - containedOrigin));

        return Orientation == Orientation.Horizontal
            ? new Rect(slot.X, containedOrigin, slot.Width, containedExtent)
            : new Rect(containedOrigin, slot.Y, containedExtent, slot.Height);
    }

    [Pure]
    private bool ScrollsPrimary() =>
        AutoScroll &&
        (ScrollBars & (Orientation == Orientation.Horizontal ? ScrollBars.Horizontal : ScrollBars.Vertical)) != 0;

    [Pure]
    private bool UsesViewportPrimaryLength(ControlBase child)
    {
        var length = Orientation == Orientation.Horizontal ? child.Width : child.Height;
        return length.Kind == LengthKind.Percent;
    }

    private Size RemeasureViewportRelativeLimits(ControlBase child, Constraint constraint, Size desired)
    {
        if (!ScrollsPrimary() || !HasViewportRelativePrimaryLimit(child))
        {
            return desired;
        }

        if (Orientation == Orientation.Horizontal)
        {
            child.ResolveWidthLimits(ScrollMeasureViewport.Width, out var minimum, out var maximum);
            var width = Math.Clamp(desired.Width, minimum, maximum);
            return width == desired.Width
                ? desired
                : MeasureChild(child, new Constraint(width.Add(child.Margin.Horizontal), constraint.Height), ScrollMeasureViewport.Width, null);
        }

        child.ResolveHeightLimits(ScrollMeasureViewport.Height, out var minimumHeight, out var maximumHeight);
        var height = Math.Clamp(desired.Height, minimumHeight, maximumHeight);
        return height == desired.Height
            ? desired
            : MeasureChild(child, new Constraint(constraint.Width, height.Add(child.Margin.Vertical)), null, ScrollMeasureViewport.Height);
    }

    [Pure]
    private bool HasViewportRelativePrimaryLimit(ControlBase child) =>
        Orientation == Orientation.Horizontal
            ? child.MinWidth.Kind == LengthKind.Percent || child.MaxWidth is { Kind: LengthKind.Percent }
            : child.MinHeight.Kind == LengthKind.Percent || child.MaxHeight is { Kind: LengthKind.Percent };
}
