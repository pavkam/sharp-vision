// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

/// <summary>Owns ListView's private realized-item presentation and virtualized extent synthesis.</summary>
/// <remarks>
/// Eager mode (<see cref="RowHeight"/> null) stacks every realized child sequentially at its own
/// measured height, matching <see cref="Layout.Stack"/>'s vertical, unspaced, non-reversed
/// arrangement - the only configuration <see cref="ListView"/> ever asks for. Setting
/// <see cref="RowHeight"/> switches to pure arithmetic placement against <see cref="ItemCount"/>,
/// so extent, scrollbar, and offset queries never require a realized row to answer.
/// <see cref="ListView"/> owns which logical indexes are realized and drives that decision from its
/// own <see cref="Container.ScrollChanged"/> subscription, never from inside measure or arrange -
/// <see cref="Container"/>'s own scrollbar-arming comment documents why lazy child creation during
/// layout cannot be allowed to recur here.
/// </remarks>
internal sealed class ListViewHost: Container
{
    /// <summary>Initializes a host that fills its parent cross-axis slot, matching <see cref="Layout.Stack"/>.</summary>
    public ListViewHost() => HorizontalAlignment = HorizontalAlignment.Stretch;

    /// <summary>Gets or sets the non-negative logical item count backing virtualized extent synthesis.</summary>
    public int ItemCount
    {
        get;
        set
        {
            Debug.Assert(value >= 0, "Item count is a non-negative logical count.");
            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    }

    /// <summary>Gets or sets the fixed per-row cell height; null keeps eager sequential arrangement.</summary>
    public int? RowHeight
    {
        get;
        set
        {
            Debug.Assert(value is null or > 0, "Row height is null or a positive cell count.");
            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    }

    /// <summary>Gets the stable, pre-offset row-geometry origin and content width committed by the
    /// last <see cref="Container.ResolveContentSlot"/> pass. Unlike an offset-shifted arrange rect,
    /// this stays valid across a scroll that changes <see cref="Container.VerticalOffset"/> without
    /// a following layout pass, letting <see cref="ListView"/> compute a freshly realized row's
    /// absolute Y as <c>RowOrigin.Y - VerticalOffset + index * RowHeight</c> when it realizes that
    /// row outside this container's own measure or arrange.</summary>
    public Rect RowOrigin => ViewportBounds;

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        if (RowHeight is int height)
        {
            // Height is pure arithmetic and never depends on which rows are realized - that is
            // the whole point of arithmetic placement. Width has no such formula: it is content-derived, so this
            // reports the widest currently realized row's natural width, the same way eager mode
            // reports the widest of every (always fully realized) row. A ListView that relies on
            // auto-width sizing while virtualized can see that width jitter across a scroll, the
            // same accepted tradeoff virtualizing panels make elsewhere - reporting the full
            // constraint instead would make a non-stretched ListView measure far wider than its
            // content and desynchronize from eager sizing.
            var realizedWidth = 0;

            foreach (var child in Children)
            {
                child.Measure(new Constraint(constraint.Width, height));

                if (child.Visibility != Visibility.Collapsed)
                {
                    realizedWidth = Math.Max(realizedWidth, child.DesiredSize.Width.Add(child.Margin.Horizontal));
                }
            }

            return new Size(realizedWidth, ItemCount.Multiply(height));
        }

        var measuredWidth = 0;
        var measuredHeight = 0;

        foreach (var child in Children)
        {
            child.Measure(new Constraint(constraint.Width, height: null));

            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            measuredHeight = measuredHeight.Add(child.DesiredSize.Height.Add(child.Margin.Vertical));
            measuredWidth = Math.Max(measuredWidth, child.DesiredSize.Width.Add(child.Margin.Horizontal));
        }

        return new Size(measuredWidth, measuredHeight);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        if (RowHeight is int height)
        {
            foreach (var child in Children)
            {
                if (child.Visibility == Visibility.Collapsed)
                {
                    continue;
                }

                var row = (ListItem) child;
                var y = bounds.Y.Add(row.Index.Multiply(height));
                child.Arrange(new Rect(bounds.X, y, bounds.Width, height), widthResolved: false, heightResolved: true);
                Debug.Assert(
                    child.DesiredSize.Height == height,
                    "A virtualized row's desired height matches the configured RowHeight contract - " +
                    "a template that disobeys it is clipped, not silently misaligned, by the fixed arrange slot above.");
            }

            return;
        }

        var origin = bounds.Y;

        foreach (var child in Children)
        {
            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            var outer = child.DesiredSize.Height.Add(child.Margin.Vertical);
            child.Arrange(new Rect(bounds.X, origin, bounds.Width, outer), widthResolved: false, heightResolved: true);
            origin = origin.Add(outer);
        }
    }
}
