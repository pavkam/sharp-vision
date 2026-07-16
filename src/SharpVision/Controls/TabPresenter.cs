// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Arranges retained tab headers in one clipped strip and selected content below its rule.</summary>
internal sealed class TabPresenter: Container
{
    private readonly TabControl _owner;
    private int _headerExtent;

    /// <summary>Initializes a private presenter for one non-null owner.</summary>
    /// <param name="owner">The owning TabControl.</param>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is null.</exception>
    internal TabPresenter(TabControl owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
    }

    /// <summary>Gets the current non-negative clipped header origin.</summary>
    internal int HeaderOffset { get; private set; }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var width = 0;
        var height = Children.Count == 0 ? 0 : Math.Min(2, constraint.Height ?? 2);

        for (var index = 0; index < Children.Count; index++)
        {
            var item = (TabItem) Children[index];
            var desired = MeasureChild(item, constraint);
            width = Add(width, item.HeaderWidth);

            if (index > 0)
            {
                width = Add(width, 1);
            }

            if (item.IsSelected)
            {
                height = Math.Max(height, desired.Height);
            }
        }

        _headerExtent = width;
        return new Size(width, height);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        var selected = _owner.SelectedItem;
        var selectedStart = 0;
        var selectedEnd = 0;
        var selectedWidth = 0;
        var natural = 0;

        foreach (var control in Children)
        {
            var item = (TabItem) control;

            if (ReferenceEquals(item, selected))
            {
                selectedStart = natural;
                selectedEnd = Add(natural, item.HeaderWidth);
                selectedWidth = item.HeaderWidth;
            }

            natural = Add(natural, Add(item.HeaderWidth, 1));
        }

        var maximumOffset = Math.Max(0, _headerExtent - bounds.Width);
        var offset = Math.Clamp(HeaderOffset, 0, maximumOffset);

        if (selected is not null && selectedWidth > bounds.Width)
        {
            offset = Math.Min(maximumOffset, Add(selectedStart, Math.Min(1, selectedWidth - 1)));
        }
        else if (selected is not null && selectedStart < offset)
        {
            offset = selectedStart;
        }
        else if (selected is not null && selectedEnd > Add(offset, bounds.Width))
        {
            offset = Math.Max(0, selectedEnd - bounds.Width);
        }

        HeaderOffset = Math.Clamp(offset, 0, maximumOffset);
        natural = 0;
        var headerHeight = Math.Min(1, bounds.Height);
        var contentTop = Add(bounds.Y, Math.Min(2, bounds.Height));
        var content = new Rect(bounds.X, contentTop, bounds.Width, Math.Max(0, bounds.Bottom - contentTop));

        foreach (var control in Children)
        {
            var item = (TabItem) control;
            var header = new Rect(
                bounds.X + natural - HeaderOffset,
                bounds.Y,
                item.HeaderWidth,
                headerHeight);
            item.SetPresentationBounds(header, content);
            ArrangeChild(item, bounds, ResolvedAxes.Both);
            natural = Add(natural, Add(item.HeaderWidth, 1));
        }
    }

    /// <inheritdoc/>
    protected override void OnRender(TerminalCanvas canvas)
    {
        if (Bounds.Width == 0 || Bounds.Height < 2)
        {
            return;
        }

        canvas.DrawLine(
            new Point(Bounds.X, Bounds.Y + 1),
            new Point(Bounds.Right - 1, Bounds.Y + 1),
            new Rune('─'),
            ResolvedStyle);

        var natural = 0;

        for (var index = 0; index < Children.Count - 1; index++)
        {
            natural = Add(natural, ((TabItem) Children[index]).HeaderWidth);
            var x = Bounds.X + natural - HeaderOffset;

            if (x >= Bounds.X && x < Bounds.Right)
            {
                _ = canvas.Draw("│", new Point(x, Bounds.Y), ResolvedStyle);
            }

            natural = Add(natural, 1);
        }
    }

    private static int Add(int left, int right)
    {
        Debug.Assert(left >= 0, "Tab strip accumulation uses non-negative extents.");
        Debug.Assert(right >= 0, "Tab strip accumulation uses non-negative extents.");
        var result = (long) left + right;
        return result >= int.MaxValue ? int.MaxValue : (int) result;
    }
}
