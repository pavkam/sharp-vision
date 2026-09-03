// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

using NonNegativeValue = JetBrains.Annotations.NonNegativeValueAttribute;

/// <summary>Consumes physical edges in child order and optionally fills the remainder.</summary>
[PublicAPI]
public sealed class Dock: Container
{
    private static readonly AttachedLayoutProperty<Dock, DockSide> _sides = new(
        DockSide.Left,
        InvalidationImpact.Measure);

    /// <summary>Initializes a dock that fills its parent layout slot.</summary>
    public Dock() => InitializePanelPresentation();

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

    /// <summary>Gets one control's attached physical side.</summary>
    /// <param name="control">The non-null control.</param>
    /// <returns>The attached side, defaulting to left.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    public static DockSide GetSide(ControlBase control)
        => _sides.Get(control);

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
        ArgumentOutOfRangeException.ThrowIfNotDefined(value, nameof(value), "The dock side is unknown.");
        _sides.Set(control, value);
    }

    /// <inheritdoc/>
    // Mirrors ResolveAxisBorders's two-phase shape so measure and arrange agree on what a Star
    // child owns. Phase 1 walks children in declaration order exactly as before for every
    // non-Star participant, preserving the tested cross-axis Percent/Cells cascade. A Star
    // participant is instead deferred: only its margin (and trailing spacing) is reserved from
    // its own axis, so it never eats a later sibling's budget the way a direct, whole-slot
    // measure would. Phase 2 then splits each axis's true leftover across its deferred Star
    // children with DistributeStarShares - the same weighted split ArrangeOverride uses - and
    // measures each one against its fair share, not the slot it merely stood in front of.
    protected override Size MeasureOverride(Constraint constraint)
    {
        var remainingWidth = constraint.Width;
        var remainingHeight = constraint.Height;
        var usedWidth = 0;
        var usedHeight = 0;
        var desiredWidth = 0;
        var desiredHeight = 0;
        var last = LastParticipant();
        List<DeferredStar>? deferred = null;
        List<int>? horizontalStarIndices = null;
        List<int>? verticalStarIndices = null;

        for (var index = 0; index < Children.Count; index++)
        {
            var child = Children[index];
            var horizontal = GetSide(child) is DockSide.Left or DockSide.Right;
            var isFillParticipant = LastChildFills && index == last;

            // A Star child only needs deferring when its own axis is bounded and it is not the
            // LastChildFills participant - both cases already measure correctly today, since an
            // unbounded slot falls back to the intrinsic path and the fill participant never
            // competes for a distributed share in ResolveAxisBorders either.
            var length = horizontal ? child.Width : child.Height;
            var axisBounded = horizontal ? remainingWidth.HasValue : remainingHeight.HasValue;

            if (child.Visibility != Visibility.Collapsed &&
                !isFillParticipant &&
                length.Kind == LengthKind.Star &&
                axisBounded)
            {
                var margin = horizontal ? child.Margin.Horizontal : child.Margin.Vertical;
                var containingExtent = horizontal ? remainingWidth!.Value : remainingHeight!.Value;
                ResolveLimits(child, containingExtent, horizontal, out var minimum, out _);

                (deferred ??= []).Add(new DeferredStar(
                    index,
                    horizontal,
                    margin,
                    minimum,
                    remainingWidth,
                    remainingHeight,
                    usedWidth,
                    usedHeight));

                if (horizontal)
                {
                    (horizontalStarIndices ??= []).Add(index);
                }
                else
                {
                    (verticalStarIndices ??= []).Add(index);
                }

                var reserved = margin.Add(index == last ? 0 : Spacing);

                if (horizontal)
                {
                    usedWidth = usedWidth.Add(reserved);
                    remainingWidth = remainingWidth.Subtract(reserved);
                }
                else
                {
                    usedHeight = usedHeight.Add(reserved);
                    remainingHeight = remainingHeight.Subtract(reserved);
                }

                continue;
            }

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
            if (isFillParticipant)
            {
                continue;
            }

            var spacing = index == last ? 0 : Spacing;

            if (horizontal)
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

        if (deferred is not null)
        {
            MeasureDeferredStars(
                deferred,
                horizontalStarIndices,
                verticalStarIndices,
                remainingWidth,
                remainingHeight,
                usedWidth,
                usedHeight,
                ref desiredWidth,
                ref desiredHeight);
        }

        return new Size(
            Math.Max(desiredWidth, usedWidth),
            Math.Max(desiredHeight, usedHeight));
    }

    // Splits each axis's true leftover (the pool phase 1 left behind, post margin/spacing)
    // across that axis's deferred Star children and measures each against its fair share.
    // A Star child's own-axis contribution to the dock's own DesiredSize is its declared
    // minimum, never its fair share - matching Tracks.ResolveCore's LengthKind.Star convention
    // of counting a Star track's minimum toward container sizing - so a Star never inflates an
    // ancestor sized to fit this dock. Every deferred Star on the same axis stacks against the
    // others there, exactly like any other sequential Dock child - its minimum is a reservation
    // on top of what the axis already needed, not an independent claim evaluated in isolation -
    // so the running total seeds from the final phase-1 used* and each Star's minimum adds to
    // it in turn, mirroring the non-star used.Add(outer) prefix-sum below. Its cross-axis
    // desired size still participates through the normal max-union, exactly as any other
    // child's does.
    private void MeasureDeferredStars(
        List<DeferredStar> deferred,
        List<int>? horizontalStarIndices,
        List<int>? verticalStarIndices,
        int? finalRemainingWidth,
        int? finalRemainingHeight,
        int usedWidth,
        int usedHeight,
        ref int desiredWidth,
        ref int desiredHeight)
    {
        int[]? widthBorders = null;
        int[]? heightBorders = null;

        if (horizontalStarIndices is not null)
        {
            widthBorders = new int[Children.Count];
            DistributeStarShares(horizontalStarIndices, finalRemainingWidth!.Value, horizontal: true, widthBorders);
        }

        if (verticalStarIndices is not null)
        {
            heightBorders = new int[Children.Count];
            DistributeStarShares(verticalStarIndices, finalRemainingHeight!.Value, horizontal: false, heightBorders);
        }

        var widthMinimumTotal = usedWidth;
        var heightMinimumTotal = usedHeight;

        foreach (var entry in deferred)
        {
            var child = Children[entry.Index];
            var border = entry.Horizontal ? widthBorders![entry.Index] : heightBorders![entry.Index];
            var ownAxis = border.Add(entry.Margin);

            child.Measure(entry.Horizontal
                ? new Constraint(ownAxis, entry.RemainingHeight)
                : new Constraint(entry.RemainingWidth, ownAxis));

            if (entry.Horizontal)
            {
                var crossOuter = child.DesiredSize.Height.Add(child.Margin.Vertical);
                widthMinimumTotal = widthMinimumTotal.Add(entry.Minimum);
                desiredWidth = Math.Max(desiredWidth, widthMinimumTotal);
                desiredHeight = Math.Max(desiredHeight, entry.UsedHeight.Add(crossOuter));
            }
            else
            {
                var crossOuter = child.DesiredSize.Width.Add(child.Margin.Horizontal);
                heightMinimumTotal = heightMinimumTotal.Add(entry.Minimum);
                desiredHeight = Math.Max(desiredHeight, heightMinimumTotal);
                desiredWidth = Math.Max(desiredWidth, entry.UsedWidth.Add(crossOuter));
            }
        }
    }

    // Captures one Star child skipped during the phase-1 walk: its own-axis reservations
    // (margin, minimum) plus a snapshot of both remaining budgets and both running union
    // prefixes at the moment it was declared, so phase 2 can measure and fold it in as if it
    // had never left its place in the sequence.
    private readonly record struct DeferredStar(
        int Index,
        bool Horizontal,
        int Margin,
        int Minimum,
        int? RemainingWidth,
        int? RemainingHeight,
        int UsedWidth,
        int UsedHeight);

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
        var horizontalBorders = ResolveAxisBorders(horizontal: true, bounds.Width, last, out var horizontalLimitBases);
        var verticalBorders = ResolveAxisBorders(horizontal: false, bounds.Height, last, out var verticalLimitBases);

        for (var index = 0; index < Children.Count; index++)
        {
            var child = Children[index];

            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            if (LastChildFills && index == last)
            {
                child.Arrange(
                    remaining,
                    widthResolved: true,
                    heightResolved: true,
                    widthLimitBase: remaining.Width,
                    heightLimitBase: remaining.Height);
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
                DockSide.Right => new Rect(remaining.Right.SaturatingSubtract(outer), remaining.Y, outer, remaining.Height),
                DockSide.Bottom => new Rect(remaining.X, remaining.Bottom.SaturatingSubtract(outer), remaining.Width, outer),
                _ => throw new UnreachableException()
            };
            // Dock resolves both axes: one from the requested edge length and
            // the other from the perpendicular space owned by the dock.
            child.Arrange(
                slot,
                widthResolved: true,
                heightResolved: true,
                widthLimitBase: horizontal ? horizontalLimitBases[index] : remaining.Width,
                heightLimitBase: horizontal ? remaining.Height : verticalLimitBases[index]);
            remaining = Consume(remaining, side, outer);

            if (index != last)
            {
                remaining = Consume(remaining, side, Math.Min(Spacing, horizontal
                    ? remaining.Width
                    : remaining.Height));
            }
        }
    }

    [Pure]
    private static Rect Consume(Rect value, DockSide side, int extent)
    {
        Debug.Assert(extent >= 0, "Consumed dock extent cannot be negative.");
        Debug.Assert(Enum.IsDefined(side), "Dock side must be defined.");

        return side switch
        {
            DockSide.Left => new Rect(value.X.Add(extent), value.Y, value.Width - extent, value.Height),
            DockSide.Top => new Rect(value.X, value.Y.Add(extent), value.Width, value.Height - extent),
            DockSide.Right => new Rect(value.X, value.Y, value.Width - extent, value.Height),
            DockSide.Bottom => new Rect(value.X, value.Y, value.Width, value.Height - extent),
            _ => throw new UnreachableException()
        };
    }

    // Resolves a non-Star edge request against the given axis. Called once per participant by
    // ResolveAxisBorders; Star lengths never reach here.
    [Pure]
    private static int Resolve(ControlBase child, int available, bool horizontal)
    {
        Debug.Assert(available >= 0, "Available dock axis space is non-negative.");

        var length = horizontal ? child.Width : child.Height;
        var desired = horizontal ? child.DesiredSize.Width : child.DesiredSize.Height;
        ResolveLimits(child, available, horizontal, out var minimum, out var maximum);
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
    [Pure]
    private int[] ResolveAxisBorders(bool horizontal, int axisTotal, int last, out int[] limitBases)
    {
        var borders = new int[Children.Count];
        limitBases = new int[Children.Count];
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
                limitBases[index] = remaining;
                var border = Resolve(child, remaining, horizontal);
                borders[index] = border;
                remaining -= Math.Min(remaining, border.Add(margin));
            }

            remaining -= Math.Min(remaining, index == last ? 0 : Spacing);
        }

        if (starIndices is not null && totalWeight > 0)
        {
            foreach (var index in starIndices)
            {
                limitBases[index] = remaining;
            }

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
            ResolveLimits(child, pool, horizontal, out var minimum, out _);
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
                ResolveLimits(child, pool, horizontal, out _, out var maximum);

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

    private static void ResolveLimits(
        ControlBase child,
        int containingExtent,
        bool horizontal,
        out int minimum,
        out int maximum)
    {
        if (horizontal)
        {
            child.ResolveWidthLimits(containingExtent, out minimum, out maximum);
        }
        else
        {
            child.ResolveHeightLimits(containingExtent, out minimum, out maximum);
        }
    }

    [Pure]
    private static int Percent(int axis, double value)
    {
        Debug.Assert(axis >= 0, "Percentage base axis is non-negative.");

        var result = Math.Round(axis * value / 100, MidpointRounding.AwayFromZero);
        return result >= int.MaxValue ? int.MaxValue : (int) result;
    }

    [Pure]
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
