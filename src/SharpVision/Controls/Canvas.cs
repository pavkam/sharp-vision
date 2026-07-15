// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using System.Runtime.CompilerServices;


/// <summary>Positions children by optional physical offsets and resolved sizes.</summary>
public sealed class Canvas: Container
{
    private static readonly ConditionalWeakTable<Control, Position> _positions = [];

    /// <summary>Initializes a canvas that fills its parent layout slot.</summary>
    public Canvas() => HorizontalAlignment = HorizontalAlignment.Stretch;

    /// <summary>Gets or sets whether descendants are clipped to Canvas bounds.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool ClipToBounds
    {
        get;
        set => _ = Set(ref field, value, Invalidation.Render);
    } = true;

    /// <inheritdoc/>
    internal override bool ClipsChildren => ClipToBounds;

    /// <summary>Gets the optional leading horizontal offset.</summary>
    /// <param name="control">The non-null control.</param>
    /// <returns>The attached offset, or null.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    public static Length? GetLeft(Control control) => GetPosition(control)?.Left;

    /// <summary>Gets the optional leading vertical offset.</summary>
    /// <param name="control">The non-null control.</param>
    /// <returns>The attached offset, or null.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    public static Length? GetTop(Control control) => GetPosition(control)?.Top;

    /// <summary>Gets the optional trailing horizontal offset.</summary>
    /// <param name="control">The non-null control.</param>
    /// <returns>The attached offset, or null.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    public static Length? GetRight(Control control) => GetPosition(control)?.Right;

    /// <summary>Gets the optional trailing vertical offset.</summary>
    /// <param name="control">The non-null control.</param>
    /// <returns>The attached offset, or null.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    public static Length? GetBottom(Control control) => GetPosition(control)?.Bottom;

    /// <summary>Sets or clears the leading horizontal offset.</summary>
    /// <param name="control">The non-null mutable control.</param>
    /// <param name="value">A cells/percent offset, or null.</param>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> is automatic or proportional.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public static void SetLeft(Control control, Length? value)
    {
        Validate(control, value);
        Position position = _positions.GetOrCreateValue(control);

        if (position.Left != value)
        {
            position.Left = value;
            InvalidateParent(control);
        }
    }

    /// <summary>Sets or clears the leading vertical offset.</summary>
    /// <param name="control">The non-null mutable control.</param>
    /// <param name="value">A cells/percent offset, or null.</param>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> is automatic or proportional.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public static void SetTop(Control control, Length? value)
    {
        Validate(control, value);
        Position position = _positions.GetOrCreateValue(control);

        if (position.Top != value)
        {
            position.Top = value;
            InvalidateParent(control);
        }
    }

    /// <summary>Sets or clears the trailing horizontal offset.</summary>
    /// <param name="control">The non-null mutable control.</param>
    /// <param name="value">A cells/percent offset, or null.</param>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> is automatic or proportional.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public static void SetRight(Control control, Length? value)
    {
        Validate(control, value);
        Position position = _positions.GetOrCreateValue(control);

        if (position.Right != value)
        {
            position.Right = value;
            InvalidateParent(control);
        }
    }

    /// <summary>Sets or clears the trailing vertical offset.</summary>
    /// <param name="control">The non-null mutable control.</param>
    /// <param name="value">A cells/percent offset, or null.</param>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> is automatic or proportional.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public static void SetBottom(Control control, Length? value)
    {
        Validate(control, value);
        Position position = _positions.GetOrCreateValue(control);

        if (position.Bottom != value)
        {
            position.Bottom = value;
            InvalidateParent(control);
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

        if (AutoScroll)
        {
            Control? bar = _bars is not null
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

        for (int index = Children.Count - 1; index >= 0; index--)
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
        int width = 0;
        int height = 0;

        foreach (Control child in Children)
        {
            child.Measure(new Constraint(width: null, height: null));

            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            Position? position = GetPosition(child);
            int outerWidth = Add(child.DesiredSize.Width, child.Margin.Horizontal);
            int outerHeight = Add(child.DesiredSize.Height, child.Margin.Vertical);
            width = Math.Max(width, Add(Add(Fixed(position?.Left), outerWidth), Fixed(position?.Right)));
            height = Math.Max(
                height,
                Add(Add(Fixed(position?.Top), outerHeight), Fixed(position?.Bottom)));
        }

        return new Size(width, height);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        foreach (Control child in Children)
        {
            if (child.Visibility == Visibility.Collapsed)
            {
                child.Arrange(default);
                continue;
            }

            Position? position = GetPosition(child);
            int left = Resolve(position?.Left, bounds.Width);
            int right = Resolve(position?.Right, bounds.Width);
            int top = Resolve(position?.Top, bounds.Height);
            int bottom = Resolve(position?.Bottom, bounds.Height);
            int width = Outer(child, horizontal: true, bounds.Width, left, right);
            int height = Outer(child, horizontal: false, bounds.Height, top, bottom);
            int x = position?.Left is not null
                ? Add(bounds.X, left)
                : position?.Right is not null
                    ? bounds.Right - right - width
                    : bounds.X;
            int y = position?.Top is not null
                ? Add(bounds.Y, top)
                : position?.Bottom is not null
                    ? bounds.Bottom - bottom - height
                    : bounds.Y;
            child.Arrange(
                new Rect(x, y, width, height),
                widthResolved: true,
                heightResolved: true);
        }
    }

    // bounds.X/bounds.Y (left, in the ArrangeOverride callers below) legitimately
    // goes negative once this canvas is scrolled content inside an armed
    // AutoScroll container: ResolveContentSlot shifts the content slot by the
    // current offset. The increment (right) — a resolved offset or margin —
    // remains non-negative in every caller.
    private static int Add(int left, int right)
    {
        Debug.Assert(right >= 0, "Canvas accumulation uses a non-negative increment.");

        long result = (long) left + right;
        return result >= int.MaxValue ? int.MaxValue : (int) result;
    }

    private static int Fixed(Length? value) => value is { Kind: Kind.Cells }
        ? (int) value.Value.Value
        : 0;

    private static Position? GetPosition(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return _positions.TryGetValue(control, out Position? position) ? position : null;
    }

    private static void InvalidateParent(Control control)
    {
        if (control.Parent is Canvas parent)
        {
            parent.Invalidate(Invalidation.Measure);
        }
    }

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

        Length length = horizontal ? child.Width : child.Height;
        int margin = horizontal ? child.Margin.Horizontal : child.Margin.Vertical;
        Position? position = GetPosition(child);

        // Dual anchored automatic children stretch between the two offsets.
        if (length.Kind == Kind.Auto &&
            (horizontal ? position?.Left : position?.Top) is not null &&
            (horizontal ? position?.Right : position?.Bottom) is not null)
        {
            return Math.Max(0, axis - leading - trailing);
        }

        int desired = horizontal ? child.DesiredSize.Width : child.DesiredSize.Height;
        int minimum = horizontal ? child.MinWidth : child.MinHeight;
        int maximum = horizontal ? child.MaxWidth : child.MaxHeight;

        int border = length.Kind switch
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

        double result = Math.Round(axis * value / 100, MidpointRounding.AwayFromZero);
        return result >= int.MaxValue ? int.MaxValue : (int) result;
    }

    private static void Validate(Control control, Length? value)
    {
        ArgumentNullException.ThrowIfNull(control);

        if (value is { Kind: Kind.Auto or Kind.Star })
        {
            throw new ArgumentException("A Canvas offset must use cells or percent.", nameof(value));
        }

        control.VerifyMutable();
    }
}
