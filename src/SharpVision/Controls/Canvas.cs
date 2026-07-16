// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Positions children by optional physical offsets and resolved sizes.</summary>
public sealed class Canvas: Container
{
    /// <summary>Initializes a canvas that fills its parent layout slot.</summary>
    public Canvas() => HorizontalAlignment = HorizontalAlignment.Stretch;

    /// <summary>Gets or sets whether descendants are clipped to Canvas bounds.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool ClipToBounds
    {
        get;
        set => _ = SetProperty(ref field, value, ChangeImpact.Render);
    } = true;

    /// <inheritdoc/>
    protected override bool ClipsChildren => ClipToBounds;

    /// <summary>Gets the leading horizontal offset from the control's own property.</summary>
    /// <param name="control">The non-null control.</param>
    /// <returns>The offset, or null.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    public static Length? GetLeft(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return control.Left;
    }

    /// <summary>Gets the leading vertical offset from the control's own property.</summary>
    /// <param name="control">The non-null control.</param>
    /// <returns>The offset, or null.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    public static Length? GetTop(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return control.Top;
    }

    /// <summary>Gets the trailing horizontal offset from the control's own property.</summary>
    /// <param name="control">The non-null control.</param>
    /// <returns>The offset, or null.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    public static Length? GetRight(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return control.Right;
    }

    /// <summary>Gets the trailing vertical offset from the control's own property.</summary>
    /// <param name="control">The non-null control.</param>
    /// <returns>The offset, or null.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    public static Length? GetBottom(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return control.Bottom;
    }

    /// <summary>Sets or clears the leading horizontal offset on the control.</summary>
    /// <param name="control">The non-null mutable control.</param>
    /// <param name="value">A cells/percent offset, or null.</param>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> is automatic or proportional.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public static void SetLeft(Control control, Length? value)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.Left = value;
    }

    /// <summary>Sets or clears the leading vertical offset on the control.</summary>
    public static void SetTop(Control control, Length? value)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.Top = value;
    }

    /// <summary>Sets or clears the trailing horizontal offset on the control.</summary>
    public static void SetRight(Control control, Length? value)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.Right = value;
    }

    /// <summary>Sets or clears the trailing vertical offset on the control.</summary>
    public static void SetBottom(Control control, Length? value)
    {
        ArgumentNullException.ThrowIfNull(control);
        control.Bottom = value;
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

        if (AutoScroll)
        {
            var bar = _bars is not null
                ? _bars[1].HitTest(point) ?? _bars[0].HitTest(point)
                : null;

            if (bar is not null)
            {
                return bar;
            }

            if (!_viewportBounds.Contains(point))
            {
                return Bounds.Contains(point) ? this : null;
            }
        }

        for (var index = Children.Count - 1; index >= 0; index--)
        {
            if (Children[index].HitTest(point) is { } child)
            {
                return child;
            }
        }

        return Bounds.Contains(point) ? this : null;
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        var width = 0;
        var height = 0;

        foreach (var child in Children)
        {
            child.Measure(new Constraint(width: null, height: null));

            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            var outerWidth = Add(child.DesiredSize.Width, child.Margin.Horizontal);
            var outerHeight = Add(child.DesiredSize.Height, child.Margin.Vertical);
            width = Math.Max(width, Add(Add(Fixed(child.Left), outerWidth), Fixed(child.Right)));
            height = Math.Max(
                height,
                Add(Add(Fixed(child.Top), outerHeight), Fixed(child.Bottom)));
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

            var left = Resolve(child.Left, bounds.Width);
            var right = Resolve(child.Right, bounds.Width);
            var top = Resolve(child.Top, bounds.Height);
            var bottom = Resolve(child.Bottom, bounds.Height);
            var width = Outer(child, horizontal: true, bounds.Width, left, right);
            var height = Outer(child, horizontal: false, bounds.Height, top, bottom);
            var x = child.Left is not null
                ? Add(bounds.X, left)
                : child.Right is not null
                    ? bounds.Right - right - width
                    : bounds.X;
            var y = child.Top is not null
                ? Add(bounds.Y, top)
                : child.Bottom is not null
                    ? bounds.Bottom - bottom - height
                    : bounds.Y;
            child.Arrange(
                new Rect(x, y, width, height),
                widthResolved: true,
                heightResolved: true);
        }
    }

    private static int Add(int left, int right)
    {
        Debug.Assert(right >= 0, "Canvas accumulation uses a non-negative increment.");

        var result = (long) left + right;
        return result >= int.MaxValue ? int.MaxValue : (int) result;
    }

    private static int Fixed(Length? value) => value is { Kind: Kind.Cells }
        ? (int) value.Value.Value
        : 0;

    private static int Outer(
        Control child,
        bool horizontal,
        int axis,
        int leading,
        int trailing)
    {
        Debug.Assert(axis >= 0, "Available canvas axis space is non-negative.");
        Debug.Assert(leading >= 0, "Leading canvas offset is non-negative.");
        Debug.Assert(trailing >= 0, "Trailing canvas offset is non-negative.");

        var length = horizontal ? child.Width : child.Height;
        var margin = horizontal ? child.Margin.Horizontal : child.Margin.Vertical;

        if (length.Kind == Kind.Auto &&
            (horizontal ? child.Left : child.Top) is not null &&
            (horizontal ? child.Right : child.Bottom) is not null)
        {
            return Math.Max(0, axis - leading - trailing);
        }

        var desired = horizontal ? child.DesiredSize.Width : child.DesiredSize.Height;
        var minimum = horizontal ? child.MinWidth : child.MinHeight;
        var maximum = horizontal ? child.MaxWidth : child.MaxHeight;

        var border = length.Kind switch
        {
            Kind.Auto => desired,
            Kind.Cells => (int) length.Value,
            Kind.Percent => Resolve(length, axis),
            Kind.Star => axis,
            _ => throw new UnreachableException(),
        };

        border = Math.Clamp(border, minimum, maximum);
        return Add(border, margin);
    }

    private static int Resolve(Length? value, int axis)
    {
        Debug.Assert(axis >= 0, "Percentage base axis is non-negative.");

        return value switch
        {
            null => 0,
            { Kind: Kind.Cells } => (int) value.Value.Value,
            { Kind: Kind.Percent } => Percent(axis, value.Value.Value),
            _ => throw new UnreachableException(),
        };
    }

    private static int Percent(int axis, double value)
    {
        Debug.Assert(axis >= 0, "Percentage base axis is non-negative.");

        var result = Math.Round(axis * value / 100, MidpointRounding.AwayFromZero);
        return result >= int.MaxValue ? int.MaxValue : (int) result;
    }
}
