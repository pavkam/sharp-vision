// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

/// <summary>Consumes physical edges in child order and optionally fills the remainder.</summary>
[PublicAPI]
public sealed class Dock: ChromeAuthoringContainer
{
    private static readonly ConditionalWeakTable<ControlBase, DockPlacement> _placements = [];

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
    public static DockSide GetSide(ControlBase control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return _placements.TryGetValue(control, out var placement) ? placement.Side : DockSide.Left;
    }

    /// <summary>Sets one control's attached physical side.</summary>
    /// <param name="control">The non-null mutable control.</param>
    /// <param name="value">The defined physical edge.</param>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public static void SetSide(ControlBase control, DockSide value)
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

            var outerWidth = child.DesiredSize.Width.Add(child.Margin.Horizontal);
            var outerHeight = child.DesiredSize.Height.Add(child.Margin.Vertical);

            // Track the union of consumed edges and the last fill participant so
            // the dock reports both intrinsic and edge-reserved minimum sizes.
            desiredWidth = Math.Max(desiredWidth, usedWidth.Add(outerWidth));
            desiredHeight = Math.Max(desiredHeight, usedHeight.Add(outerHeight));

            // The final participant keeps the remaining slot and does not consume
            // space during measure when LastChildFills is enabled.
            if (LastChildFills && index == last)
            {
                continue;
            }

            var spacing = index == last ? 0 : Spacing;

            if (GetSide(child) is DockSide.Left or DockSide.Right)
            {
                var consumed = outerWidth.Add(spacing);
                usedWidth = usedWidth.Add(consumed);
                remainingWidth = remainingWidth.Subtract(consumed);
            }
            else
            {
                var consumed = outerHeight.Add(spacing);
                usedHeight = usedHeight.Add(consumed);
                remainingHeight = remainingHeight.Subtract(consumed);
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

        // Every same-axis participant's size is decided once, up front, by ResolveAxisBorders —
        // both non-Star children (as Resolve always did) and Star children, which share their
        // axis's leftover space by weight, mirroring Tracks.AllocateStars for Grid/StackPanel.
        // Horizontal (Left/Right) and vertical (Top/Bottom) children never consume each other's
        // axis, so the two groups are resolved independently up front.
        var horizontalBorders = ResolveAxisBorders(horizontal: true, bounds.Width, last);
        var verticalBorders = ResolveAxisBorders(horizontal: false, bounds.Height, last);

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
            var horizontal = side is DockSide.Left or DockSide.Right;
            var axis = horizontal ? remaining.Width : remaining.Height;
            var margin = horizontal ? child.Margin.Horizontal : child.Margin.Vertical;
            var border = (horizontal ? horizontalBorders : verticalBorders)[index];
            var outer = Math.Min(axis, border.Add(margin));

            var slot = side switch
            {
                DockSide.Left => new Rect(remaining.X, remaining.Y, outer, remaining.Height),
                DockSide.Top => new Rect(remaining.X, remaining.Y, remaining.Width, outer),
                DockSide.Right => new Rect(remaining.Right - outer, remaining.Y, outer, remaining.Height),
                DockSide.Bottom => new Rect(remaining.X, remaining.Bottom - outer, remaining.Width, outer),
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

    private static Rect Consume(Rect value, DockSide side, int extent)
    {
        Debug.Assert(extent >= 0, "Consumed dock extent cannot be negative.");
        Debug.Assert(Enum.IsDefined(side), "Dock side must be defined.");

        return side switch
        {
            DockSide.Left => new Rect(value.X + extent, value.Y, value.Width - extent, value.Height),
            DockSide.Top => new Rect(value.X, value.Y + extent, value.Width, value.Height - extent),
            DockSide.Right => new Rect(value.X, value.Y, value.Width - extent, value.Height),
            DockSide.Bottom => new Rect(value.X, value.Y, value.Width, value.Height - extent),
            _ => throw new UnreachableException()
        };
    }

    // Resolves a non-Star edge request against the given axis. Called once per participant by
    // ResolveAxisBorders; Star lengths never reach here.
    private static int Resolve(ControlBase child, int available, bool horizontal)
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
            LengthKind.Auto => desired,
            LengthKind.Cells => (int) length.Value,
            LengthKind.Percent => Percent(available, length.Value),
            LengthKind.Star => throw new UnreachableException("Star lengths resolve through AllocateStarBorders."),
            _ => throw new UnreachableException()
        };

        return Math.Min(space, Math.Clamp(requested, minimum, maximum));
    }

    // Resolves every same-axis participant's border in one sequential pass: a non-Star child
    // exactly as Resolve always did, and every Star child by splitting whatever axis space is
    // left over by weight. A non-Star child deliberately resolves against the space left after
    // every OTHER non-Star participant only — never against a Star's own share. Star shares are
    // themselves only known once every non-Star border on the axis is settled, so letting a later
    // Percent see a preceding Star's real rendered share would make its own resolution depend on
    // a value that in turn depends on it, and that circular system has no stable interior fixed
    // point: iterating it drives the Star's share toward the whole axis and the
    // Percent's toward zero, regardless of where either sits in the declared order. Resolving
    // every non-Star border against a Star-blind "remaining" instead keeps the two declaration
    // orders symmetric — a Star before or after a Percent claiming the same nominal share yields
    // the same split. ArrangeOverride still visually reserves each Star's real allocated share
    // when it walks children in render order; only the SIZE each non-Star sibling resolves to is
    // decided here, independent of where any Star sits in the sequence. Only participants are
    // considered: a collapsed child, or the last child when LastChildFills, never reserves space
    // on either axis.
    private int[] ResolveAxisBorders(bool horizontal, int axisTotal, int last)
    {
        var borders = new int[Children.Count];
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
            var onThisAxis = horizontal ? side is DockSide.Left or DockSide.Right : side is DockSide.Top or DockSide.Bottom;

            if (!onThisAxis)
            {
                continue;
            }

            var length = horizontal ? child.Width : child.Height;
            var margin = horizontal ? child.Margin.Horizontal : child.Margin.Vertical;

            if (length.Kind == LengthKind.Star)
            {
                (starIndices ??= []).Add(index);
                totalWeight += length.Value;
                remaining -= Math.Min(remaining, margin);
            }
            else
            {
                var border = Resolve(child, remaining, horizontal);
                borders[index] = border;
                remaining -= Math.Min(remaining, border.Add(margin));
            }

            remaining -= Math.Min(remaining, index == last ? 0 : Spacing);
        }

        if (starIndices is not null && totalWeight > 0)
        {
            DistributeStarShares(starIndices, remaining, horizontal, borders);
        }

        return borders;
    }

    // Splits one axis's leftover pool across its Star participants by weight, re-running the
    // split whenever a Min/Max limit clips a share so the clipped remainder reaches the eligible
    // siblings instead of vanishing or landing on an unrelated child — mirroring
    // Tracks.AllocateStars's redistribution loop for Grid/StackPanel. Every Star is
    // first seeded at its own minimum, exactly as Tracks pre-seeds a track's destination before
    // splitting the remainder: a large minimum is a reservation the weighted split then adds to,
    // not a post-hoc floor that silently steals cells another Star was already allocated.
    private void DistributeStarShares(List<int> starIndices, int pool, bool horizontal, int[] borders)
    {
        var reserved = 0;

        foreach (var index in starIndices)
        {
            var child = Children[index];
            var minimum = horizontal ? child.MinWidth : child.MinHeight;
            borders[index] = minimum;
            reserved += minimum;
        }

        var remaining = Math.Max(0, pool - reserved);
        var eligible = new HashSet<int>(starIndices);

        while (remaining > 0 && eligible.Count > 0)
        {
            var totalWeight = 0d;

            foreach (var index in eligible)
            {
                var length = horizontal ? Children[index].Width : Children[index].Height;
                totalWeight += length.Value;
            }

            if (totalWeight <= 0)
            {
                return;
            }

            var pass = remaining;
            var cumulativeWeight = 0d;
            var previousEdge = 0;
            var distributed = 0;
            List<int>? saturated = null;

            foreach (var index in starIndices)
            {
                if (!eligible.Contains(index))
                {
                    continue;
                }

                var child = Children[index];
                var length = horizontal ? child.Width : child.Height;
                var maximum = horizontal ? child.MaxWidth : child.MaxHeight;

                cumulativeWeight += length.Value;
                var edge = (int) Math.Round(pass * cumulativeWeight / totalWeight, MidpointRounding.AwayFromZero);
                var share = Math.Max(0, edge - previousEdge);
                previousEdge = edge;

                var capacity = Math.Max(0, maximum - borders[index]);
                var added = Math.Min(share, capacity);
                borders[index] += added;
                distributed += added;

                if (added < share)
                {
                    (saturated ??= []).Add(index);
                }
            }

            if (distributed == 0)
            {
                return;
            }

            remaining -= distributed;

            if (saturated is not null)
            {
                foreach (var index in saturated)
                {
                    _ = eligible.Remove(index);
                }
            }
        }
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
