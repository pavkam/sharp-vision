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
    public Overlay() => InitializePanelPresentation();

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
    protected internal override bool ClipsDescendantVisualOverflow => AutoScroll || ClipToBounds;

    /// <inheritdoc/>
    /// <remarks>
    /// Deliberately bypasses <see cref="GetChildOrder"/>: default focus navigation stays in
    /// collection order regardless of z-order, matching every documented navigation contract and
    /// the pinned <c>MoveNext_WhenZOrderDiffers_UsesCollectionOrderAsync</c> regression. Only hit
    /// testing and rendering honor z-order. The lookup still walks every navigation-eligible owned
    /// slot in registration order - the overlay's own <see cref="ControlBase.ContextMenu"/> slot and
    /// any framework popup slot included - because the inherited <see cref="ControlBase.NavigationCount"/>
    /// counts those same slots, and a count that outruns the lookup would fault Tab traversal.
    /// </remarks>
    protected internal override ControlBase NavigationAt(int index) => OwnedControls.NavigationAt(index);

    /// <inheritdoc/>
    /// <remarks>Deliberately bypasses <see cref="GetChildOrder"/> for the same reason as <see
    /// cref="NavigationAt"/>: selectable text reads in collection order, not z-order.</remarks>
    protected internal override bool AddSelectableTextChildren(List<ControlBase> children)
    {
        ArgumentNullException.ThrowIfNull(children);
        children.AddRange(Children);
        return true;
    }

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
    protected internal override bool HitTestsSelf => HitTestsOwnBounds;

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var width = 0;
        var height = 0;

        foreach (var child in Children)
        {
            var positionsWidth = GetLeft(child) is not null || GetRight(child) is not null;
            var positionsHeight = GetTop(child) is not null || GetBottom(child) is not null;
            _ = MeasureChild(
                child,
                new Constraint(
                    positionsWidth ? null : constraint.Width,
                    positionsHeight ? null : constraint.Height));

            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            var outerWidth = child.OuterDesiredSize.Width;
            var outerHeight = child.OuterDesiredSize.Height;
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
        // A scrolling overlay axis arranges within bounds inflated to Math.Max(Extent, Viewport) by
        // Container.ResolveContentSlot, so a Percent/Star offset or length's true (viewport-relative)
        // value must resolve against the visible Viewport instead of that inflated axis - otherwise
        // it is crushed toward its own automatic size the moment overflowing content makes Extent
        // exceed Viewport, mirroring the same fix already applied to Grid, Stack, and Dock. Only the
        // offset/length percentage base changes; the physical placement below (bounds.X, bounds.Right,
        // and the un-anchored fill width/height) still spans the real content box so a child still
        // lays out at the correct physical position within it.
        var horizontalBase = ScrollsHorizontally() ? Viewport.Width : bounds.Width;
        var verticalBase = ScrollsVertically() ? Viewport.Height : bounds.Height;

        foreach (var child in Children)
        {
            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            var positionsWidth = GetLeft(child) is not null || GetRight(child) is not null;
            var positionsHeight = GetTop(child) is not null || GetBottom(child) is not null;
            var left = positionsWidth ? Resolve(GetLeft(child), horizontalBase) : 0;
            var right = positionsWidth ? Resolve(GetRight(child), horizontalBase) : 0;
            var top = positionsHeight ? Resolve(GetTop(child), verticalBase) : 0;
            var bottom = positionsHeight ? Resolve(GetBottom(child), verticalBase) : 0;
            var width = positionsWidth
                ? Outer(child, horizontal: true, horizontalBase, left, right)
                : bounds.Width;
            var height = positionsHeight
                ? Outer(child, horizontal: false, verticalBase, top, bottom)
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

            var resolvedAxes = (positionsWidth ? ResolvedAxes.Width : ResolvedAxes.None) |
                (positionsHeight ? ResolvedAxes.Height : ResolvedAxes.None);
            ArrangeChild(
                child,
                slot,
                resolvedAxes,
                widthLimitBase: positionsWidth ? horizontalBase : null,
                heightLimitBase: positionsHeight ? verticalBase : null);
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
            ResolveChildWidthLimits(child, axis, out var minimum, out var maximum);
            return ResolveAxis(child, length, margin, minimum, maximum, horizontal, axis, leading, trailing);
        }

        ResolveChildHeightLimits(child, axis, out var verticalMinimum, out var verticalMaximum);
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
            { Kind: LengthKind.Percent } => Length.ResolvePercent(axis, value.Value.Value),
            _ => throw new UnreachableException()
        };
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Orders children ascending by <see cref="GetZIndex"/>, so index <c>0</c> (the back-most
    /// slot <see cref="Container.GetChildOrder"/> defines) is the lowest z-order child and the
    /// last index (the front-most slot) is the highest. This drives hit testing and both render
    /// passes; <see cref="NavigationAt"/> and <see cref="AddSelectableTextChildren"/> deliberately
    /// override this container's default wiring to keep reading collection order instead.
    /// </remarks>
    protected override void GetChildOrder(Span<int> indices)
    {
        for (var index = 0; index < indices.Length; index++)
        {
            indices[index] = index;
        }

        // Insertion sort is stable for equal z-values and avoids comparer or
        // tuple allocation for the small layer sets common in terminal UIs.
        for (var index = 1; index < indices.Length; index++)
        {
            var current = indices[index];
            var currentZ = GetZIndex(Children[current]);
            var insertion = index - 1;

            while (insertion >= 0 && GetZIndex(Children[indices[insertion]]) > currentZ)
            {
                indices[insertion + 1] = indices[insertion];
                insertion--;
            }

            indices[insertion + 1] = current;
        }
    }
}
