// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

/// <summary>Arranges owned children in one shared box with stable layering.</summary>
[PublicAPI]
public sealed class Overlay: Container
{
    private static readonly AttachedLayoutProperty<Overlay, Length?> _left = new(
        null,
        InvalidationImpact.Measure);
    private static readonly AttachedLayoutProperty<Overlay, Length?> _top = new(
        null,
        InvalidationImpact.Measure);
    private static readonly AttachedLayoutProperty<Overlay, Length?> _right = new(
        null,
        InvalidationImpact.Measure);
    private static readonly AttachedLayoutProperty<Overlay, Length?> _bottom = new(
        null,
        InvalidationImpact.Measure);
    private static readonly AttachedLayoutProperty<Overlay, int> _zIndices = new(
        0,
        InvalidationImpact.Render);

    /// <summary>Initializes an overlay that fills its parent shared box.</summary>
    public Overlay()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        EnableChromeAuthoring();
    }

    /// <summary>Gets or sets whether descendants are clipped to overlay bounds.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool ClipToBounds
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.Render);
    } = true;

    /// <summary>Gets whether empty overlay cells target this Overlay after child hit testing.</summary>
    internal bool HitTestsOwnBounds { get; init; } = true;

    /// <inheritdoc/>
    protected override bool ClipsChildren => ClipToBounds;

    /// <inheritdoc/>
    internal override bool ClipsDescendantVisualOverflow => AutoScroll || ClipToBounds;

    /// <summary>Gets one control's attached leading horizontal offset.</summary>
    /// <param name="control">The non-null control.</param>
    /// <returns>The attached offset, or null when unset.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    public static Length? GetLeft(ControlBase control)
        => _left.Get(control);

    /// <summary>Gets one control's attached leading vertical offset.</summary>
    /// <param name="control">The non-null control.</param>
    /// <returns>The attached offset, or null when unset.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    public static Length? GetTop(ControlBase control)
        => _top.Get(control);

    /// <summary>Gets one control's attached trailing horizontal offset.</summary>
    /// <param name="control">The non-null control.</param>
    /// <returns>The attached offset, or null when unset.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    public static Length? GetRight(ControlBase control)
        => _right.Get(control);

    /// <summary>Gets one control's attached trailing vertical offset.</summary>
    /// <param name="control">The non-null control.</param>
    /// <returns>The attached offset, or null when unset.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    public static Length? GetBottom(ControlBase control)
        => _bottom.Get(control);

    /// <summary>Sets or clears one control's attached leading horizontal offset.</summary>
    /// <param name="control">The non-null mutable control.</param>
    /// <param name="value">A cells/percent offset, or null.</param>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> is automatic or proportional.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public static void SetLeft(ControlBase control, Length? value)
    {
        ArgumentNullException.ThrowIfNull(control);
        ValidatePositionOffset(value);
        _left.Set(control, value);
    }

    /// <summary>Sets or clears one control's attached leading vertical offset.</summary>
    /// <param name="control">The non-null mutable control.</param>
    /// <param name="value">A cells/percent offset, or null.</param>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> is automatic or proportional.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public static void SetTop(ControlBase control, Length? value)
    {
        ArgumentNullException.ThrowIfNull(control);
        ValidatePositionOffset(value);
        _top.Set(control, value);
    }

    /// <summary>Sets or clears one control's attached trailing horizontal offset.</summary>
    /// <param name="control">The non-null mutable control.</param>
    /// <param name="value">A cells/percent offset, or null.</param>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> is automatic or proportional.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public static void SetRight(ControlBase control, Length? value)
    {
        ArgumentNullException.ThrowIfNull(control);
        ValidatePositionOffset(value);
        _right.Set(control, value);
    }

    /// <summary>Sets or clears one control's attached trailing vertical offset.</summary>
    /// <param name="control">The non-null mutable control.</param>
    /// <param name="value">A cells/percent offset, or null.</param>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> is automatic or proportional.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public static void SetBottom(ControlBase control, Length? value)
    {
        ArgumentNullException.ThrowIfNull(control);
        ValidatePositionOffset(value);
        _bottom.Set(control, value);
    }

    private static void ValidatePositionOffset(Length? value)
    {
        if (value is { Kind: LengthKind.Auto or LengthKind.Star })
        {
            throw new ArgumentException("Position offsets must use cells or percentage values.", nameof(value));
        }
    }

    /// <summary>Gets one control's attached signed z-order.</summary>
    /// <param name="control">The non-null control.</param>
    /// <returns>The attached value, or zero when unset.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    public static int GetZIndex(ControlBase control)
        => _zIndices.Get(control);

    /// <summary>Sets one control's attached signed z-order.</summary>
    /// <param name="control">The non-null mutable control.</param>
    /// <param name="value">The signed layer order.</param>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public static void SetZIndex(ControlBase control, int value)
        => _zIndices.Set(control, value);

    /// <inheritdoc/>
    internal override ControlBase? HitTest(Point point) =>
        CanHitTestSelf(point, requireContainment: false)
            ? HitTestPopup(point) ?? (AutoScroll ? HitTestScrollable(point) : HitTestUnscrolled(point))
            : null;

    private ControlBase? HitTestScrollable(Point point) => Bounds.Contains(point)
        ? HitTestBars(point) ??
          (ViewportBounds.Contains(point) ? HitTestOrderedContent(point) : null) ??
          (HitTestsOwnBounds ? this : null)
        : null;

    private ControlBase? HitTestUnscrolled(Point point) =>
        !ClipToBounds || Bounds.Contains(point)
            ? HitTestOrderedContent(point) ?? (HitTestsOwnBounds && Bounds.Contains(point) ? this : null)
            : null;

    private ControlBase? HitTestBars(Point point)
    {
        if (Bars is null)
        {
            return null;
        }

        for (var index = Bars.Count - 1; index >= 0; index--)
        {
            if (Bars[index].HitTest(point) is { } bar)
            {
                return bar;
            }
        }

        return null;
    }

    private ControlBase? HitTestOrderedContent(Point point)
    {
        var rented = RentOrdered();

        try
        {
            for (var index = Children.Count - 1; index >= 0; index--)
            {
                if (rented[index].HitTest(point) is { } child)
                {
                    return child;
                }
            }
        }
        finally
        {
            ArrayPool<ControlBase>.Shared.Return(rented, clearArray: true);
        }

        return null;
    }

    /// <inheritdoc/>
    internal override ControlBase? HitTestPopupCore(Point point)
    {
        var rented = RentOrdered();

        try
        {
            for (var index = Children.Count - 1; index >= 0; index--)
            {
                if (rented[index].HitTestPopupBranch(point, OwnedControlLayer.Normal) is { } popup)
                {
                    return popup;
                }
            }
        }
        finally
        {
            ArrayPool<ControlBase>.Shared.Return(rented, clearArray: true);
        }

        return null;
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var width = 0;
        var height = 0;

        foreach (var child in Children)
        {
            var positionsWidth = GetLeft(child) is not null || GetRight(child) is not null;
            var positionsHeight = GetTop(child) is not null || GetBottom(child) is not null;
            child.Measure(
                new Constraint(
                    positionsWidth ? null : constraint.Width,
                    positionsHeight ? null : constraint.Height));

            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            var outerWidth = child.DesiredSize.Width.Add(child.Margin.Horizontal);
            var outerHeight = child.DesiredSize.Height.Add(child.Margin.Vertical);
            var desiredWidth = positionsWidth
                ? Fixed(GetLeft(child)).Add(outerWidth).Add(Fixed(GetRight(child)))
                : outerWidth;
            var desiredHeight = positionsHeight
                ? Fixed(GetTop(child)).Add(outerHeight).Add(Fixed(GetBottom(child)))
                : outerHeight;
            width = Math.Max(width, desiredWidth);
            height = Math.Max(height, desiredHeight);
        }

        return new Size(width, height);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        foreach (var child in Children)
        {
            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            var positionsWidth = GetLeft(child) is not null || GetRight(child) is not null;
            var positionsHeight = GetTop(child) is not null || GetBottom(child) is not null;
            var left = positionsWidth ? Resolve(GetLeft(child), bounds.Width) : 0;
            var right = positionsWidth ? Resolve(GetRight(child), bounds.Width) : 0;
            var top = positionsHeight ? Resolve(GetTop(child), bounds.Height) : 0;
            var bottom = positionsHeight ? Resolve(GetBottom(child), bounds.Height) : 0;
            var width = positionsWidth
                ? Outer(child, horizontal: true, bounds.Width, left, right)
                : bounds.Width;
            var height = positionsHeight
                ? Outer(child, horizontal: false, bounds.Height, top, bottom)
                : bounds.Height;
            var x = GetLeft(child) is not null
                ? bounds.X.Add(left)
                : GetRight(child) is not null
                    ? TrailingOrigin(bounds.Right, right, width)
                    : bounds.X;
            var y = GetTop(child) is not null
                ? bounds.Y.Add(top)
                : GetBottom(child) is not null
                    ? TrailingOrigin(bounds.Bottom, bottom, height)
                    : bounds.Y;

            var slot = new Rect(x, y, width, height);

            if (child is IOverlayPositionConstraint constraint)
            {
                slot = constraint.ConstrainOverlaySlot(slot, bounds);
            }

            child.Arrange(
                slot,
                widthResolved: positionsWidth,
                heightResolved: positionsHeight,
                widthLimitBase: positionsWidth ? bounds.Width : null,
                heightLimitBase: positionsHeight ? bounds.Height : null);
        }
    }

    [Pure]
    private static int Fixed(Length? value) => value is { Kind: LengthKind.Cells }
        ? (int) value.Value.Value
        : 0;

    [Pure]
    private static int Outer(
        ControlBase child,
        bool horizontal,
        int axis,
        int leading,
        int trailing)
    {
        Debug.Assert(axis >= 0, "Available overlay axis space is non-negative.");
        Debug.Assert(leading >= 0, "Leading overlay offset is non-negative.");
        Debug.Assert(trailing >= 0, "Trailing overlay offset is non-negative.");

        var length = horizontal ? child.Width : child.Height;
        var margin = horizontal ? child.Margin.Horizontal : child.Margin.Vertical;
        if (horizontal)
        {
            child.ResolveWidthLimits(axis, out var minimum, out var maximum);
            return ResolveAxis(child, length, margin, minimum, maximum, horizontal, axis, leading, trailing);
        }

        child.ResolveHeightLimits(axis, out var verticalMinimum, out var verticalMaximum);
        return ResolveAxis(child, length, margin, verticalMinimum, verticalMaximum, horizontal, axis, leading, trailing);
    }

    private static int ResolveAxis(
        ControlBase child,
        Length length,
        int margin,
        int minimum,
        int maximum,
        bool horizontal,
        int axis,
        int leading,
        int trailing)
    {

        if (length.Kind is LengthKind.Auto or LengthKind.Star &&
            (horizontal ? GetLeft(child) : GetTop(child)) is not null &&
            (horizontal ? GetRight(child) : GetBottom(child)) is not null)
        {
            // Both offsets are hard boundaries for the child's margin box (mirrors CSS absolute
            // positioning with both inset edges set): margin is deflated from the offset-to-offset
            // extent before Min/Max clamp the resulting border-box candidate, then margin is added
            // back so Arrange's own Margin.Deflate reproduces this box exactly between the two
            // offsets. This differs from the single-offset Star arm below, whose far edge is not a
            // hard boundary and may legitimately extend past it by margin.
            var contentCandidate = (int) Math.Clamp(
                (long) StretchedExtent(axis, leading, trailing) - margin, 0, int.MaxValue);
            return Math.Clamp(contentCandidate, minimum, maximum).Add(margin);
        }

        var desired = horizontal ? child.DesiredSize.Width : child.DesiredSize.Height;

        var border = length.Kind switch
        {
            LengthKind.Auto => desired,
            LengthKind.Cells => (int) length.Value,
            LengthKind.Percent => Resolve(length, axis),
            LengthKind.Star => StretchedExtent(axis, leading, trailing),
            _ => throw new UnreachableException()
        };

        border = Math.Clamp(border, minimum, maximum);
        return border.Add(margin);
    }

    // Independently valid offsets and extents may sum past the signed integer
    // range, so subtractive geometry widens before it saturates.
    [Pure]
    private static int StretchedExtent(int axis, int leading, int trailing) =>
        (int) Math.Clamp((long) axis - leading - trailing, 0, int.MaxValue);

    [Pure]
    private static int TrailingOrigin(int edge, int offset, int extent) =>
        (int) Math.Clamp((long) edge - offset - extent, int.MinValue, int.MaxValue);

    [Pure]
    private static int Resolve(Length? value, int axis)
    {
        Debug.Assert(axis >= 0, "Percentage base axis is non-negative.");

        return value switch
        {
            null => 0,
            { Kind: LengthKind.Cells } => (int) value.Value.Value,
            { Kind: LengthKind.Percent } => Percent(axis, value.Value.Value),
            _ => throw new UnreachableException()
        };
    }

    [Pure]
    private static int Percent(int axis, double value)
    {
        Debug.Assert(axis >= 0, "Percentage base axis is non-negative.");

        var result = Math.Round(axis * value / 100, MidpointRounding.AwayFromZero);
        return result >= int.MaxValue ? int.MaxValue : (int) result;
    }

    /// <inheritdoc/>
    internal override void RenderContent(TerminalCanvas canvas, Rect contentClip)
    {
        var rented = RentOrdered();

        try
        {
            for (var index = 0; index < Children.Count; index++)
            {
                if (rented[index].RendersInNormalLayer)
                {
                    rented[index].Render(canvas, contentClip);
                }
            }
        }
        finally
        {
            ArrayPool<ControlBase>.Shared.Return(rented, clearArray: true);
        }
    }

    /// <inheritdoc/>
    internal override void RenderOwnedPopupDescendants(TerminalCanvas canvas)
    {
        var rented = RentOrdered();

        try
        {
            for (var index = 0; index < Children.Count; index++)
            {
                rented[index].RenderPopupBranch(canvas, OwnedControlLayer.Normal);
            }
        }
        finally
        {
            ArrayPool<ControlBase>.Shared.Return(rented, clearArray: true);
        }
    }

    private ControlBase[] RentOrdered()
    {
        Debug.Assert(Children.Count >= 0, "Overlay child count cannot be negative.");

        var result = ArrayPool<ControlBase>.Shared.Rent(Children.Count);

        for (var index = 0; index < Children.Count; index++)
        {
            result[index] = Children[index];
        }

        // Insertion sort is stable for equal z-values and avoids comparer or
        // tuple allocation for the small layer sets common in terminal UIs.
        for (var index = 1; index < Children.Count; index++)
        {
            var current = result[index];
            var currentZ = GetZIndex(current);
            var insertion = index - 1;

            while (insertion >= 0 && GetZIndex(result[insertion]) > currentZ)
            {
                result[insertion + 1] = result[insertion];
                insertion--;
            }

            result[insertion + 1] = current;
        }

        Debug.Assert(result.Length >= Children.Count, "Rented overlay buffer must cover every child.");
        return result;
    }
}
