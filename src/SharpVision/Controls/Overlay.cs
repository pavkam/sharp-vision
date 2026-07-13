// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using System.Buffers;
using System.Runtime.CompilerServices;

using SharpVision.Layout;
using SharpVision.Terminal.Geometry;

using TerminalCanvas = TerminalCanvas;

/// <summary>Arranges owned children in one shared box with stable layering.</summary>
public sealed class Overlay: Container
{
    private static readonly ConditionalWeakTable<Control, ZOrder> _orders = [];

    /// <summary>Initializes an overlay that fills its parent shared box.</summary>
    public Overlay() => HorizontalAlignment = HorizontalAlignment.Stretch;

    /// <summary>Gets or sets whether descendants are clipped to overlay bounds.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool ClipToBounds
    {
        get;
        set => _ = Set(ref field, value, Invalidation.Render);
    } = true;

    /// <inheritdoc/>
    internal override bool ClipsChildren => ClipToBounds;

    /// <summary>Gets one control's attached signed z-order.</summary>
    /// <param name="control">The non-null control.</param>
    /// <returns>The attached value, or zero when unset.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    public static int GetZIndex(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return _orders.TryGetValue(control, out ZOrder? order) ? order.Value : 0;
    }

    /// <summary>Sets one control's attached signed z-order.</summary>
    /// <param name="control">The non-null mutable control.</param>
    /// <param name="value">The signed layer order.</param>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public static void SetZIndex(Control control, int value)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.VerifyMutable();
        ZOrder order = _orders.GetOrCreateValue(control);

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
    public override Control? HitTest(Point point)
    {
        if (IsDisposed ||
            !IsHitTestVisible ||
            !EffectiveIsVisible ||
            !EffectiveIsEnabled ||
            (ClipToBounds && !Bounds.Contains(point)))
        {
            return null;
        }

        if (HitTestPopup(point) is { } popup)
        {
            return popup;
        }

        Control[] rented = RentOrdered();

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
            ArrayPool<Control>.Shared.Return(rented, clearArray: true);
        }

        return Bounds.Contains(point) ? this : null;
    }

    /// <inheritdoc/>
    protected override Size MeasureCore(Constraint constraint)
    {
        var width = 0;
        var height = 0;

        foreach (Control child in Children)
        {
            child.Measure(constraint);

            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            width = Math.Max(width, Add(child.DesiredSize.Width, child.Margin.Horizontal));
            height = Math.Max(height, Add(child.DesiredSize.Height, child.Margin.Vertical));
        }

        return new Size(width, height);
    }

    /// <inheritdoc/>
    protected override void ArrangeCore(Rect bounds)
    {
        foreach (Control child in Children)
        {
            child.Arrange(bounds);
        }
    }

    /// <inheritdoc/>
    internal override void RenderChildren(TerminalCanvas canvas)
    {
        Control[] rented = RentOrdered();

        try
        {
            for (var index = 0; index < Children.Count; index++)
            {
                rented[index].Render(canvas);
            }
        }
        finally
        {
            ArrayPool<Control>.Shared.Return(rented, clearArray: true);
        }

        if (Parent is null)
        {
            RenderPopupLayer(canvas);
        }
    }

    private static int Add(int left, int right)
    {
        var result = (long) left + right;
        return result >= int.MaxValue ? int.MaxValue : (int) result;
    }

    private Control[] RentOrdered()
    {
        Control[] result = ArrayPool<Control>.Shared.Rent(Children.Count);

        for (var index = 0; index < Children.Count; index++)
        {
            result[index] = Children[index];
        }

        // Insertion sort is stable for equal z-values and avoids comparer or
        // tuple allocation for the small layer sets common in terminal UIs.
        for (var index = 1; index < Children.Count; index++)
        {
            Control current = result[index];
            var currentZ = GetZIndex(current);
            var insertion = index - 1;

            while (insertion >= 0 && GetZIndex(result[insertion]) > currentZ)
            {
                result[insertion + 1] = result[insertion];
                insertion--;
            }

            result[insertion + 1] = current;
        }

        return result;
    }
}
