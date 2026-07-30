// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

/// <summary>Consumes physical edges in child order and optionally fills the remainder.</summary>
[PublicAPI]
public sealed class Dock: Container
{
    /// <summary>Gets or sets the complete locally authored border.</summary>
    public new Border Border { get => base.Border; set => base.Border = value; }

    /// <summary>Returns border ownership to the active Theme.</summary>
    public new void ResetBorder() => base.ResetBorder();

    /// <summary>Gets or sets the complete locally authored shadow.</summary>
    public new Shadow Shadow { get => base.Shadow; set => base.Shadow = value; }

    /// <summary>Returns shadow ownership to the active Theme.</summary>
    public new void ResetShadow() => base.ResetShadow();

    private static readonly ConditionalWeakTable<Control, DockPlacement> _placements = [];

    /// <summary>Initializes a dock that fills its parent layout slot.</summary>
    public Dock() => HorizontalAlignment = HorizontalAlignment.Stretch;

    /// <summary>Gets or sets whether the last non-collapsed child fills remaining space.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public bool LastChildFills
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.Measure);
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
            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
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
    protected override Size MeasureOverride(Constraint constraint)
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

            var outerWidth = LayoutMath.Add(child.DesiredSize.Width, child.Margin.Horizontal);
            var outerHeight = LayoutMath.Add(child.DesiredSize.Height, child.Margin.Vertical);

            // Track the union of consumed edges and the last fill participant so
            // the dock reports both intrinsic and edge-reserved minimum sizes.
            desiredWidth = Math.Max(desiredWidth, LayoutMath.Add(usedWidth, outerWidth));
            desiredHeight = Math.Max(desiredHeight, LayoutMath.Add(usedHeight, outerHeight));

            // The final participant keeps the remaining slot and does not consume
            // space during measure when LastChildFills is enabled.
            if (LastChildFills && index == last)
            {
                continue;
            }

            var spacing = index == last ? 0 : Spacing;

            if (GetSide(child) is Side.Left or Side.Right)
            {
                var consumed = LayoutMath.Add(outerWidth, spacing);
                usedWidth = LayoutMath.Add(usedWidth, consumed);
                remainingWidth = LayoutMath.Subtract(remainingWidth, consumed);
            }
            else
            {
                var consumed = LayoutMath.Add(outerHeight, spacing);
                usedHeight = LayoutMath.Add(usedHeight, consumed);
                remainingHeight = LayoutMath.Subtract(remainingHeight, consumed);
            }
        }

        return new Size(
            Math.Max(desiredWidth, usedWidth),
            Math.Max(desiredHeight, usedHeight));
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        var remaining = bounds;
        var last = LastParticipant();

        // Star children share their axis's remaining space by weight instead of each
        // claiming the whole remainder in child order, mirroring Tracks.AllocateStars for
        // Grid/StackPanel. Horizontal (Left/Right) and vertical (Top/Bottom) children never
        // consume each other's axis, so the two groups are allocated independently up front.
        var horizontalStars = AllocateStarBorders(horizontal: true, bounds.Width, last);
        var verticalStars = AllocateStarBorders(horizontal: false, bounds.Height, last);

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
            var length = horizontal ? child.Width : child.Height;
            var border = length.Kind == Kind.Star
                ? (horizontal ? horizontalStars : verticalStars)[index]
                : Resolve(child, axis, horizontal);
            var outer = Math.Min(axis, LayoutMath.Add(border, margin));

            var slot = side switch
            {
                Side.Left => new Rect(remaining.X, remaining.Y, outer, remaining.Height),
                Side.Top => new Rect(remaining.X, remaining.Y, remaining.Width, outer),
                Side.Right => new Rect(remaining.Right - outer, remaining.Y, outer, remaining.Height),
                Side.Bottom => new Rect(remaining.X, remaining.Bottom - outer, remaining.Width, outer),
                _ => throw new UnreachableException()
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

    private static Rect Consume(Rect value, Side side, int extent)
    {
        Debug.Assert(extent >= 0, "Consumed dock extent cannot be negative.");
        Debug.Assert(Enum.IsDefined(side), "Dock side must be defined.");

        return side switch
        {
            Side.Left => new Rect(value.X + extent, value.Y, value.Width - extent, value.Height),
            Side.Top => new Rect(value.X, value.Y + extent, value.Width, value.Height - extent),
            Side.Right => new Rect(value.X, value.Y, value.Width - extent, value.Height),
            Side.Bottom => new Rect(value.X, value.Y, value.Width, value.Height - extent),
            _ => throw new UnreachableException()
        };
    }

    // Resolves a non-Star edge request. Star children are allocated separately by
    // AllocateStarBorders, sharing their axis's remaining space by weight.
    private static int Resolve(Control child, int available, bool horizontal)
    {
        Debug.Assert(available >= 0, "Available dock axis space is non-negative.");

        var length = horizontal ? child.Width : child.Height;
        var desired = horizontal ? child.DesiredSize.Width : child.DesiredSize.Height;
        var minimum = horizontal ? child.MinWidth : child.MinHeight;
        var maximum = horizontal ? child.MaxWidth : child.MaxHeight;
        var margin = horizontal ? child.Margin.Horizontal : child.Margin.Vertical;

        // Margins reserve their own cells outside the resolved edge request.
        var space = Math.Max(0, available - margin);

        var requested = length.Kind switch
        {
            Kind.Auto => desired,
            Kind.Cells => (int) length.Value,
            Kind.Percent => Percent(available, length.Value),
            Kind.Star => throw new UnreachableException("Star lengths resolve through AllocateStarBorders."),
            _ => throw new UnreachableException()
        };

        return Math.Min(space, Math.Clamp(requested, minimum, maximum));
    }

    // Computes, per Star child sharing one axis (horizontal: Left/Right, vertical: Top/Bottom),
    // its proportional share of that axis's space left over after every non-Star participant on
    // the same axis is resolved in child order — the same order ArrangeOverride itself walks, so
    // this dry run reproduces the identical shrinking "available" sequence Percent-length siblings
    // already depend on. Only participants are considered: a collapsed child, or the last child
    // when LastChildFills, never reserves space on either axis.
    private int[] AllocateStarBorders(bool horizontal, int axisTotal, int last)
    {
        var shares = new int[Children.Count];
        var remaining = axisTotal;
        List<int>? starIndices = null;
        var totalWeight = 0d;

        for (var index = 0; index < Children.Count; index++)
        {
            var child = Children[index];

            if (child.Visibility == Visibility.Collapsed || (LastChildFills && index == last))
            {
                continue;
            }

            var side = GetSide(child);
            var onThisAxis = horizontal ? side is Side.Left or Side.Right : side is Side.Top or Side.Bottom;

            if (!onThisAxis)
            {
                continue;
            }

            var length = horizontal ? child.Width : child.Height;
            var margin = horizontal ? child.Margin.Horizontal : child.Margin.Vertical;

            if (length.Kind == Kind.Star)
            {
                (starIndices ??= []).Add(index);
                totalWeight += length.Value;
                remaining -= Math.Min(remaining, margin);
            }
            else
            {
                var border = Resolve(child, remaining, horizontal);
                remaining -= Math.Min(remaining, LayoutMath.Add(border, margin));
            }

            remaining -= Math.Min(remaining, index == last ? 0 : Spacing);
        }

        if (starIndices is null || totalWeight <= 0)
        {
            return shares;
        }

        var cumulativeWeight = 0d;
        var previousEdge = 0;

        foreach (var index in starIndices)
        {
            var child = Children[index];
            var length = horizontal ? child.Width : child.Height;
            var minimum = horizontal ? child.MinWidth : child.MinHeight;
            var maximum = horizontal ? child.MaxWidth : child.MaxHeight;

            cumulativeWeight += length.Value;
            var edge = (int) Math.Round(remaining * cumulativeWeight / totalWeight, MidpointRounding.AwayFromZero);
            var share = edge - previousEdge;
            previousEdge = edge;

            shares[index] = Math.Clamp(share, minimum, maximum);
        }

        return shares;
    }

    private static int Percent(int axis, double value)
    {
        Debug.Assert(axis >= 0, "Percentage base axis is non-negative.");

        var result = Math.Round(axis * value / 100, MidpointRounding.AwayFromZero);
        return result >= int.MaxValue ? int.MaxValue : (int) result;
    }

    private int LastParticipant()
    {
        for (var index = Children.Count - 1; index >= 0; index--)
        {
            if (Children[index].Visibility != Visibility.Collapsed)
            {
                Debug.Assert(index >= 0 && index < Children.Count, "Last participant index must be valid.");
                return index;
            }
        }

        return -1;
    }
}
