namespace SharpVision.Controls;

using System.Diagnostics;
using System.Runtime.CompilerServices;

using SharpVision.Layout;
using SharpVision.Terminal.Geometry;

/// <summary>Consumes physical edges in child order and optionally fills the remainder.</summary>
public sealed class Dock: Container
{
    private static readonly ConditionalWeakTable<Control, DockPlacement> _placements = [];

    /// <summary>Initializes a dock that fills its parent layout slot.</summary>
    public Dock() => HorizontalAlignment = HorizontalAlignment.Stretch;

    /// <summary>Gets or sets whether the last non-collapsed child fills remaining space.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool LastChildFills
    {
        get;
        set => _ = Set(ref field, value, Invalidation.Measure);
    } = true;

    /// <summary>Gets or sets non-negative cells after each consumed child.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public int Spacing
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _ = Set(ref field, value, Invalidation.Measure);
        }
    }

    /// <summary>Gets one control's attached physical side.</summary>
    /// <param name="control">The non-null control.</param>
    /// <returns>The attached side, defaulting to left.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    public static Side GetSide(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return _placements.TryGetValue(control, out var placement) ? placement.Side : Side.Left;
    }

    /// <summary>Sets one control's attached physical side.</summary>
    /// <param name="control">The non-null mutable control.</param>
    /// <param name="value">The defined physical edge.</param>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public static void SetSide(Control control, Side value)
    {
        ArgumentNullException.ThrowIfNull(control);

        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "The dock side is unknown.");
        }

        control.VerifyMutable();
        var placement = _placements.GetOrCreateValue(control);

        if (placement.Side == value)
        {
            return;
        }

        placement.Side = value;

        if (control.Parent is Dock parent)
        {
            parent.Invalidate(Invalidation.Measure);
        }
    }

    /// <inheritdoc/>
    protected override Size MeasureCore(Constraint constraint)
    {
        var remainingWidth = constraint.Width;
        var remainingHeight = constraint.Height;
        var usedWidth = 0;
        var usedHeight = 0;
        var desiredWidth = 0;
        var desiredHeight = 0;
        var last = LastParticipant();

        for (var index = 0; index < Children.Count; index++)
        {
            var child = Children[index];
            child.Measure(new Constraint(remainingWidth, remainingHeight));

            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            var outerWidth = Add(child.DesiredSize.Width, child.Margin.Horizontal);
            var outerHeight = Add(child.DesiredSize.Height, child.Margin.Vertical);
            desiredWidth = Math.Max(desiredWidth, Add(usedWidth, outerWidth));
            desiredHeight = Math.Max(desiredHeight, Add(usedHeight, outerHeight));

            if (LastChildFills && index == last)
            {
                continue;
            }

            var spacing = index == last ? 0 : Spacing;

            if (GetSide(child) is Side.Left or Side.Right)
            {
                var consumed = Add(outerWidth, spacing);
                usedWidth = Add(usedWidth, consumed);
                remainingWidth = Subtract(remainingWidth, consumed);
            }
            else
            {
                var consumed = Add(outerHeight, spacing);
                usedHeight = Add(usedHeight, consumed);
                remainingHeight = Subtract(remainingHeight, consumed);
            }
        }

        return new Size(
            Math.Max(desiredWidth, usedWidth),
            Math.Max(desiredHeight, usedHeight));
    }

    /// <inheritdoc/>
    protected override void ArrangeCore(Rect bounds)
    {
        var remaining = bounds;
        var last = LastParticipant();

        for (var index = 0; index < Children.Count; index++)
        {
            var child = Children[index];

            if (child.Visibility == Visibility.Collapsed)
            {
                child.Arrange(default);
                continue;
            }

            if (LastChildFills && index == last)
            {
                child.Arrange(remaining, widthResolved: true, heightResolved: true);
                continue;
            }

            var side = GetSide(child);
            var horizontal = side is Side.Left or Side.Right;
            var axis = horizontal ? remaining.Width : remaining.Height;
            var margin = horizontal ? child.Margin.Horizontal : child.Margin.Vertical;
            var border = Resolve(child, axis, horizontal);
            var outer = Math.Min(axis, Add(border, margin));
            var slot = side switch
            {
                Side.Left => new Rect(remaining.X, remaining.Y, outer, remaining.Height),
                Side.Top => new Rect(remaining.X, remaining.Y, remaining.Width, outer),
                Side.Right => new Rect(remaining.Right - outer, remaining.Y, outer, remaining.Height),
                Side.Bottom => new Rect(remaining.X, remaining.Bottom - outer, remaining.Width, outer),
                _ => throw new UnreachableException(),
            };
            // Dock resolves both axes: one from the requested edge length and
            // the other from the perpendicular space owned by the dock.
            child.Arrange(slot, widthResolved: true, heightResolved: true);
            remaining = Consume(remaining, side, outer);

            if (index != last)
            {
                remaining = Consume(remaining, side, Math.Min(Spacing, horizontal
                    ? remaining.Width
                    : remaining.Height));
            }
        }
    }

    private static int Add(int left, int right)
    {
        var result = (long) left + right;
        return result >= int.MaxValue ? int.MaxValue : (int) result;
    }

    private static Rect Consume(Rect value, Side side, int extent) => side switch
    {
        Side.Left => new Rect(value.X + extent, value.Y, value.Width - extent, value.Height),
        Side.Top => new Rect(value.X, value.Y + extent, value.Width, value.Height - extent),
        Side.Right => new Rect(value.X, value.Y, value.Width - extent, value.Height),
        Side.Bottom => new Rect(value.X, value.Y, value.Width, value.Height - extent),
        _ => throw new UnreachableException(),
    };

    private static int? Subtract(int? value, int consumed) => value.HasValue
        ? Math.Max(0, value.Value - consumed)
        : null;

    private static int Resolve(Control child, int available, bool horizontal)
    {
        var length = horizontal ? child.Width : child.Height;
        var desired = horizontal ? child.DesiredSize.Width : child.DesiredSize.Height;
        var minimum = horizontal ? child.MinWidth : child.MinHeight;
        var maximum = horizontal ? child.MaxWidth : child.MaxHeight;
        var margin = horizontal ? child.Margin.Horizontal : child.Margin.Vertical;
        var space = Math.Max(0, available - margin);
        var requested = length.Kind switch
        {
            Kind.Auto => desired,
            Kind.Cells => (int) length.Value,
            Kind.Percent => Percent(available, length.Value),
            Kind.Star => space,
            _ => throw new UnreachableException(),
        };

        return Math.Min(space, Math.Clamp(requested, minimum, maximum));
    }

    private static int Percent(int axis, double value)
    {
        var result = Math.Round(axis * value / 100, MidpointRounding.AwayFromZero);
        return result >= int.MaxValue ? int.MaxValue : (int) result;
    }

    private int LastParticipant()
    {
        for (var index = Children.Count - 1; index >= 0; index--)
        {
            if (Children[index].Visibility != Visibility.Collapsed)
            {
                return index;
            }
        }

        return -1;
    }
}
