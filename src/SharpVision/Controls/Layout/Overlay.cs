// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

/// <summary>Arranges owned children in one shared box with stable layering.</summary>
[PublicAPI]
public sealed class Overlay: ChromeAuthoringContainer
{
    private static readonly ConditionalWeakTable<ControlBase, OverlayZIndex> _orders = [];
    private static readonly ConditionalWeakTable<ControlBase, OverlayPosition> _positions = [];

    /// <summary>Initializes an overlay that fills its parent shared box.</summary>
    public Overlay() => HorizontalAlignment = HorizontalAlignment.Stretch;

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
    {
        ArgumentNullException.ThrowIfNull(control);
        return _positions.TryGetValue(control, out var position) ? position.Left : null;
    }

    /// <summary>Gets one control's attached leading vertical offset.</summary>
    /// <param name="control">The non-null control.</param>
    /// <returns>The attached offset, or null when unset.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    public static Length? GetTop(ControlBase control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return _positions.TryGetValue(control, out var position) ? position.Top : null;
    }

    /// <summary>Gets one control's attached trailing horizontal offset.</summary>
    /// <param name="control">The non-null control.</param>
    /// <returns>The attached offset, or null when unset.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    public static Length? GetRight(ControlBase control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return _positions.TryGetValue(control, out var position) ? position.Right : null;
    }

    /// <summary>Gets one control's attached trailing vertical offset.</summary>
    /// <param name="control">The non-null control.</param>
    /// <returns>The attached offset, or null when unset.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    public static Length? GetBottom(ControlBase control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return _positions.TryGetValue(control, out var position) ? position.Bottom : null;
    }

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
        control.VerifyMutable();
        var position = _positions.GetOrCreateValue(control);

        if (position.Left == value)
        {
            return;
        }

        position.Left = value;
        InvalidateParent(control);
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
        control.VerifyMutable();
        var position = _positions.GetOrCreateValue(control);

        if (position.Top == value)
        {
            return;
        }

        position.Top = value;
        InvalidateParent(control);
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
        control.VerifyMutable();
        var position = _positions.GetOrCreateValue(control);

        if (position.Right == value)
        {
            return;
        }

        position.Right = value;
        InvalidateParent(control);
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
        control.VerifyMutable();
        var position = _positions.GetOrCreateValue(control);

        if (position.Bottom == value)
        {
            return;
        }

        position.Bottom = value;
        InvalidateParent(control);
    }

    private static void ValidatePositionOffset(Length? value)
    {
        if (value is { Kind: LengthKind.Auto or LengthKind.Star })
        {
            throw new ArgumentException("Position offsets must use cells or percentage values.", nameof(value));
        }
    }

    private static void InvalidateParent(ControlBase control)
    {
        if (control.Parent is Overlay parent)
        {
            parent.Invalidate(Invalidation.Measure);
        }
    }

    /// <summary>Gets one control's attached signed z-order.</summary>
    /// <param name="control">The non-null control.</param>
    /// <returns>The attached value, or zero when unset.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    public static int GetZIndex(ControlBase control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return _orders.TryGetValue(control, out var order) ? order.Value : 0;
    }

    /// <summary>Sets one control's attached signed z-order.</summary>
    /// <param name="control">The non-null mutable control.</param>
    /// <param name="value">The signed layer order.</param>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public static void SetZIndex(ControlBase control, int value)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.VerifyMutable();
        var order = _orders.GetOrCreateValue(control);

        if (order.Value == value)
        {
            return;
        }

        order.Value = value;

        if (control.Parent is Overlay parent)
        {
            parent.Invalidate(Invalidation.Render);
        }
    }

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
                child.Arrange(default);
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
                heightResolved: positionsHeight);
        }
    }

    private static int Fixed(Length? value) => value is { Kind: LengthKind.Cells }
        ? (int) value.Value.Value
        : 0;

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

        if (length.Kind == LengthKind.Auto &&
            (horizontal ? GetLeft(child) : GetTop(child)) is not null &&
            (horizontal ? GetRight(child) : GetBottom(child)) is not null)
        {
            return StretchedExtent(axis, leading, trailing);
        }

        var desired = horizontal ? child.DesiredSize.Width : child.DesiredSize.Height;
        var minimum = horizontal ? child.MinWidth : child.MinHeight;
        var maximum = horizontal ? child.MaxWidth : child.MaxHeight;

        var border = length.Kind switch
        {
            LengthKind.Auto => desired,
            LengthKind.Cells => (int) length.Value,
            LengthKind.Percent => Resolve(length, axis),
            LengthKind.Star => axis,
            _ => throw new UnreachableException()
        };

        border = Math.Clamp(border, minimum, maximum);
        return border.Add(margin);
    }

    // Independently valid offsets and extents may sum past the signed integer
    // range, so subtractive geometry widens before it saturates.
    private static int StretchedExtent(int axis, int leading, int trailing) =>
        (int) Math.Clamp((long) axis - leading - trailing, 0, int.MaxValue);

    private static int TrailingOrigin(int edge, int offset, int extent) =>
        (int) Math.Clamp((long) edge - offset - extent, int.MinValue, int.MaxValue);

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
