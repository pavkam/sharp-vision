// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

using NonNegativeValue = JetBrains.Annotations.NonNegativeValueAttribute;

/// <summary>Arranges owned children in successive lines along a terminal-cell axis.</summary>
[PublicAPI]
public sealed class Wrap: Container
{
    /// <summary>Initializes a wrap panel that fills its parent cross-axis slot.</summary>
    public Wrap() => InitializePanelPresentation();

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
            FillMeasuredParticipants(children, outerSizes, constraint, PercentageBase(constraint));
            return WrapLayout.Pack(
                outerSizes,
                PrimaryPackingLimit(constraint),
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
            FillMeasuredParticipants(children, outerSizes, constraint, new Constraint(Viewport.Width, Viewport.Height));
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
                    widthRequestBase: WidthRequestBase(bounds),
                    heightRequestBase: HeightRequestBase(bounds),
                    widthLimitBase: WidthLimitBase(bounds),
                    heightLimitBase: HeightLimitBase(bounds));
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
        Constraint constraint,
        Constraint percentageBase)
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
            var desired = MeasureParticipant(child, constraint, percentageBase);
            outerSizes[index] = new Size(
                desired.Width.Add(child.Margin.Horizontal),
                desired.Height.Add(child.Margin.Vertical));
            index++;
        }

        Debug.Assert(index == children.Length, "Every non-collapsed child is a wrap participant.");
    }

    [Pure]
    private Constraint PercentageBase(Constraint constraint) => new(
        ScrollMeasureViewport.Width ?? constraint.Width,
        ScrollMeasureViewport.Height ?? constraint.Height);

    [Pure]
    private int? PrimaryPackingLimit(Constraint constraint) =>
        ScrollsPrimary() ? null : Orientation == Orientation.Horizontal ? constraint.Width : constraint.Height;

    [Pure]
    private int Primary(Rect bounds) =>
        Orientation == Orientation.Horizontal ? bounds.Width : bounds.Height;

    private Size MeasureParticipant(ControlBase child, Constraint constraint, Constraint percentageBase) =>
        MeasureChild(
            child,
            new Constraint(
                ScrollsHorizontally() ? null : constraint.Width,
                ScrollsVertically() ? null : constraint.Height),
            ScrollsHorizontally() ? percentageBase.Width : null,
            ScrollsVertically() ? percentageBase.Height : null,
            ScrollsHorizontally() ? percentageBase.Width : null,
            ScrollsVertically() ? percentageBase.Height : null);

    [Pure]
    private int? PrimaryPackingLimit(Rect bounds) =>
        ScrollsPrimary() ? null : Primary(bounds);

    [Pure]
    private int? WidthLimitBase(Rect bounds) =>
        ScrollsHorizontally() ? Viewport.Width : bounds.Width;

    [Pure]
    private int WidthRequestBase(Rect bounds) =>
        ScrollsHorizontally() ? Viewport.Width : bounds.Width;

    [Pure]
    private int? HeightLimitBase(Rect bounds) =>
        ScrollsVertically() ? Viewport.Height : bounds.Height;

    [Pure]
    private int HeightRequestBase(Rect bounds) =>
        ScrollsVertically() ? Viewport.Height : bounds.Height;

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
        Orientation == Orientation.Horizontal ? ScrollsHorizontally() : ScrollsVertically();

    [Pure]
    private bool ScrollsHorizontally() => AutoScroll && (ScrollBars & ScrollBars.Horizontal) != 0;

    [Pure]
    private bool ScrollsVertically() => AutoScroll && (ScrollBars & ScrollBars.Vertical) != 0;

}
