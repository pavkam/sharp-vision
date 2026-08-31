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
                Primary(bounds),
                Orientation,
                Spacing,
                LineSpacing,
                outerSlots);

            for (var index = 0; index < count; index++)
            {
                var slot = outerSlots[index];
                ArrangeChild(
                    children[index],
                    new Rect(bounds.X.Add(slot.X), bounds.Y.Add(slot.Y), slot.Width, slot.Height),
                    Orientation == Orientation.Horizontal ? ResolvedAxes.Width : ResolvedAxes.Height);
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
            var desired = MeasureChild(child, constraint);
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
}
