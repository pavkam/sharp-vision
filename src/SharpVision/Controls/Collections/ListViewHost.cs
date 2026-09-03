// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

using NonNegativeValue = JetBrains.Annotations.NonNegativeValueAttribute;

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
    [NonNegativeValue]
    public int ItemCount
    {
        get;
        set
        {
            Debug.Assert(value >= 0, "Item count is a non-negative logical count.");
            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    }

    private int? _rowHeight;

    /// <summary>Gets or sets the fixed per-row cell height; null keeps eager sequential arrangement.</summary>
    public int? RowHeight
    {
        get => _rowHeight;
        set
        {
            Debug.Assert(value is null or > 0, "Row height is null or a positive cell count.");
            _ = SetProperty(ref _rowHeight, value, InvalidationImpact.Measure);
        }
    }

    /// <summary>Commits a resolved per-row cell height from inside the owning <see cref="ListView"/>'s
    /// own measure or arrange transaction.</summary>
    /// <remarks>
    /// The public setter requests a measure pass through the ordinary ancestor-propagating
    /// invalidation, which is right outside layout but wrong inside it: the owner is about to
    /// measure and arrange this host synchronously with the new value anyway, and propagating the
    /// request re-dirties every ancestor after the pass that just ran. When measure resolves a
    /// provisional height from its constraint and arrange then re-resolves a different one from
    /// the final viewport - routine for a percentage row inside a parent that arranges the list
    /// smaller than it measured (a scrolling Stack arranges Percent against its viewport while
    /// measuring unbounded, so measure only ever sees MaxHeight) - that propagation repeats on
    /// every pass and the tree never reaches idle. Marking only this host dirty guarantees the
    /// owner's synchronous re-measure observes the new value without scheduling ancestor work.
    /// </remarks>
    /// <param name="value">The positive resolved row height.</param>
    internal void SetRowHeightWithinLayout(int value)
    {
        Debug.Assert(value > 0, "A resolved row height is a positive cell count.");

        if (_rowHeight == value)
        {
            return;
        }

        _rowHeight = value;
        InvalidateSelf(Invalidation.Measure);
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

    /// <summary>Arranges one virtualized row into its fixed-height slot, shared by this container's
    /// own arrange pass and <see cref="ListView"/>'s out-of-band Rewindow re-arrange. Collapsed
    /// content arranges to a zero-height rect instead of the full slot: <see cref="ListItem"/>
    /// already reports zero <see cref="ControlBase.DesiredSize"/> for collapsed content, but
    /// nothing else mirrors that onto the wrapper, so a stale full-height
    /// <see cref="ControlBase.Bounds"/> would otherwise keep hit-testing at the old slot. Sibling
    /// row placement math is untouched - a collapsed row still occupies its arithmetic slot, it is
    /// simply empty within it.</summary>
    /// <param name="row">The realized row to arrange.</param>
    /// <param name="slot">The row's full-height arithmetic slot.</param>
    internal static void ArrangeRow(ListItem row, Rect slot)
    {
        if (row.IsContentCollapsed)
        {
            row.Arrange(new Rect(slot.X, slot.Y, slot.Width, 0), widthResolved: false, heightResolved: true);
            return;
        }

        row.Arrange(slot, widthResolved: false, heightResolved: true);
        Debug.Assert(
            row.DesiredSize.Height == slot.Height,
            "A virtualized row's desired height matches the configured RowHeight contract - " +
            "a template that disobeys it is clipped, not silently misaligned, by the fixed arrange slot above.");
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
                ArrangeRow(row, new Rect(bounds.X, y, bounds.Width, height));
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
