// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies exact rendered content clipping and scrollbar chrome placement when an armed
/// container nests inside another - not merely the offset/extent arithmetic
/// <c>ContainerScrollNestingTests.cs</c> covers, but the actual painted frame: which cells show
/// content, which are blank, and which cells hit-test as which container's scrollbar, at both
/// nesting levels simultaneously.</summary>
public sealed class ContainerScrollNestingRenderTests
{
    /// <summary>Verifies only the inner container renders a scrollbar when only its own content
    /// overflows - the outer container's total content fits exactly, so it adds no chrome of its
    /// own - and content past the inner container's own viewport edge never renders anywhere,
    /// including into the outer container's own unrelated rows.</summary>
    [Fact]
    public void Render_WhenOnlyInnerContentOverflows_InnerAloneShowsScrollbarAndClipsAtItsOwnEdge()
    {
        var top = new ProbeControl(new Size(1, 1)) { Content = "T".AsMemory() };
        var a = new ProbeControl(new Size(1, 1)) { Content = "A".AsMemory() };
        var b = new ProbeControl(new Size(1, 1)) { Content = "B".AsMemory() };
        var c = new ProbeControl(new Size(1, 1)) { Content = "C".AsMemory() };
        var inner = new Stack
        {
            Width = Length.Cells(2),
            Height = Length.Cells(2),
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            Children = { a, b, c }
        };
        var outer = new Stack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            Children = { top, inner }
        };

        // outer's own total content (1 + inner's own fixed height 2 = 3) exactly fills a 3-row
        // viewport, so outer needs no bar of its own; inner's own content (a, b, c = 3 rows)
        // overflows its own fixed 2-row height, so inner alone needs one.
        new LayoutEngine().Layout(outer, new Size(2, 3));
        using Frame frame = new(new Size(2, 3));

        outer.Render(frame.Canvas);

        // Row 0: top's own content in column 0; column 1 is outer's own unreserved gutter next
        // to a control that never claimed it - blank, not scrollbar chrome or leaked content.
        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("T");
        FrameOracle.Get(frame, new Point(1, 0)).ShouldBe(string.Empty);
        outer.HitTest(new Point(1, 0)).ShouldNotBeOfType<ScrollBar>();

        // Rows 1-2: inner's own translated 2-row viewport shows only 'A' and 'B' - 'C' is past
        // inner's own edge and must never render anywhere in the frame, not even into outer's
        // own row 0 gutter or beyond the frame's own bounds.
        FrameOracle.Get(frame, new Point(0, 1)).ShouldBe("A");
        FrameOracle.Get(frame, new Point(0, 2)).ShouldBe("B");

        for (var y = 0; y < 3; y++)
        {
            for (var x = 0; x < 2; x++)
            {
                FrameOracle.Get(frame, new Point(x, y)).ShouldNotBe("C");
            }
        }

        // Inner's own scrollbar column, translated by outer's arrangement (top's own height, 1):
        // absolute rows 1-2 hit-test as inner's own vertical bar.
        outer.HitTest(new Point(1, 1)).ShouldBeOfType<ScrollBar>().Orientation.ShouldBe(Orientation.Vertical);
        outer.HitTest(new Point(1, 2)).ShouldBeOfType<ScrollBar>().Orientation.ShouldBe(Orientation.Vertical);
    }

    /// <summary>Verifies the mirror image of the previous test: the inner container's own content
    /// fits its own bounds exactly (no inner bar at all), but the outer container's combined
    /// content overflows its own viewport, so only the outer container shows a scrollbar - and
    /// hit-testing the translated inner block never reports a bar that does not exist.</summary>
    [Fact]
    public void Render_WhenOnlyOuterContentOverflows_OuterAloneShowsScrollbarAndInnerHasNone()
    {
        var top = new ProbeControl(new Size(1, 1)) { Content = "T".AsMemory() };
        var a = new ProbeControl(new Size(1, 1)) { Content = "A".AsMemory() };
        var b = new ProbeControl(new Size(1, 1)) { Content = "B".AsMemory() };
        var bottom = new ProbeControl(new Size(1, 1)) { Content = "X".AsMemory() };
        var inner = new Stack
        {
            Width = Length.Cells(1),
            Height = Length.Cells(2),
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            Children = { a, b }
        };
        var outer = new Stack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            Children = { top, inner, bottom }
        };

        // inner's own content (a, b = 2 rows) exactly fills its own fixed 2-row height - no
        // overflow, no bar. outer's own total content (top 1 + inner 2 + bottom 1 = 4) overflows
        // a 3-row viewport, so outer alone needs a bar; the reserved column narrows outer's own
        // content width by one, but inner's own explicit Width(1) is unaffected by that.
        new LayoutEngine().Layout(outer, new Size(2, 3));
        using Frame frame = new(new Size(2, 3));

        outer.Render(frame.Canvas);

        // Unscrolled: row 0 shows top, rows 1-2 show inner's own full a/b (nothing clipped,
        // since inner needs no scrolling of its own).
        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("T");
        FrameOracle.Get(frame, new Point(0, 1)).ShouldBe("A");
        FrameOracle.Get(frame, new Point(0, 2)).ShouldBe("B");

        // 'bottom' (outer's 4th row of content) is past outer's own 3-row viewport and must not
        // render anywhere in the unscrolled frame.
        for (var y = 0; y < 3; y++)
        {
            for (var x = 0; x < 2; x++)
            {
                FrameOracle.Get(frame, new Point(x, y)).ShouldNotBe("X");
            }
        }

        // outer's own bar occupies the reserved column across every row; inner's own translated
        // block (rows 1-2, column 0 only) hit-tests as content, not as any bar of its own - the
        // only bar anywhere is outer's.
        outer.HitTest(new Point(1, 0)).ShouldBeOfType<ScrollBar>().Orientation.ShouldBe(Orientation.Vertical);
        outer.HitTest(new Point(1, 1)).ShouldBeOfType<ScrollBar>().Orientation.ShouldBe(Orientation.Vertical);
        outer.HitTest(new Point(1, 2)).ShouldBeOfType<ScrollBar>().Orientation.ShouldBe(Orientation.Vertical);
        outer.HitTest(new Point(0, 1)).ShouldNotBeOfType<ScrollBar>();
    }

    /// <summary>Verifies both containers show independent scrollbars simultaneously when both
    /// overflow, each at its own correctly translated position, and scrolling one does not
    /// disturb the other's own offset, chrome, or the content clip boundary between them. Three
    /// columns are provisioned deliberately: one for content, one for inner's own bar, and one
    /// for outer's own bar - two columns would starve inner's explicit Width down to a single
    /// column once outer's own bar reservation narrows the cross-axis slot inner is arranged
    /// within, leaving no room for inner's own content and bar to coexist.</summary>
    [Fact]
    public void Render_WhenBothContainersOverflow_EachShowsItsOwnIndependentScrollbar()
    {
        var top = new ProbeControl(new Size(1, 1)) { Content = "T".AsMemory() };
        var a = new ProbeControl(new Size(1, 1)) { Content = "A".AsMemory() };
        var b = new ProbeControl(new Size(1, 1)) { Content = "B".AsMemory() };
        var c = new ProbeControl(new Size(1, 1)) { Content = "C".AsMemory() };
        var bottom = new ProbeControl(new Size(1, 1)) { Content = "X".AsMemory() };
        var inner = new Stack
        {
            Width = Length.Cells(2),
            Height = Length.Cells(2),
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            Children = { a, b, c }
        };
        var outer = new Stack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            Children = { top, inner, bottom }
        };

        // inner's own content (3 rows) overflows its own 2-row height. outer's own total content
        // (top 1 + inner 2 + bottom 1 = 4) overflows a 3-row viewport too - both need their own
        // independent bar, each claiming its own column out of the 3 provisioned.
        new LayoutEngine().Layout(outer, new Size(3, 3));
        using Frame frame = new(new Size(3, 3));

        outer.Render(frame.Canvas);

        // Unscrolled: row 0 is top, rows 1-2 are inner's own translated viewport showing 'A'/'B'
        // (its own 'C' clipped); 'bottom' is past outer's own viewport and invisible.
        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("T");
        FrameOracle.Get(frame, new Point(0, 1)).ShouldBe("A");
        FrameOracle.Get(frame, new Point(0, 2)).ShouldBe("B");

        for (var y = 0; y < 3; y++)
        {
            for (var x = 0; x < 3; x++)
            {
                FrameOracle.Get(frame, new Point(x, y)).ShouldNotBe("C");
                FrameOracle.Get(frame, new Point(x, y)).ShouldNotBe("X");
            }
        }

        // Column 1 hit-tests as inner's own bar for its own translated rows (1-2), and column 2
        // hit-tests as outer's own bar for every row - the two reservations sit in adjacent but
        // distinct columns and never collide or clip each other's chrome.
        outer.HitTest(new Point(1, 1)).ShouldBeOfType<ScrollBar>().Orientation.ShouldBe(Orientation.Vertical);
        outer.HitTest(new Point(1, 2)).ShouldBeOfType<ScrollBar>().Orientation.ShouldBe(Orientation.Vertical);
        outer.HitTest(new Point(2, 0)).ShouldBeOfType<ScrollBar>().Orientation.ShouldBe(Orientation.Vertical);
        outer.HitTest(new Point(2, 1)).ShouldBeOfType<ScrollBar>().Orientation.ShouldBe(Orientation.Vertical);
        outer.HitTest(new Point(2, 2)).ShouldBeOfType<ScrollBar>().Orientation.ShouldBe(Orientation.Vertical);

        // row 0 at column 1 is outer's own gutter next to 'top', which never claimed that
        // column - not a bar cell, since inner's own bar has no rows there to occupy.
        outer.HitTest(new Point(1, 0)).ShouldNotBeOfType<ScrollBar>();

        // Scrolling inner alone must not touch outer's own offset or bring 'bottom' into view.
        // ScrollBy commits the offset property immediately, but the resulting Arrange is
        // deferred - a real layout pass must run before the new offset is reflected in Bounds.
        _ = inner.ScrollBy(0, 1);
        outer.VerticalOffset.ShouldBe(0);
        new LayoutEngine().Layout(outer, new Size(3, 3));
        using Frame afterInnerScroll = new(new Size(3, 3));
        outer.Render(afterInnerScroll.Canvas);

        FrameOracle.Get(afterInnerScroll, new Point(0, 0)).ShouldBe("T");
        FrameOracle.Get(afterInnerScroll, new Point(0, 1)).ShouldBe("B");
        FrameOracle.Get(afterInnerScroll, new Point(0, 2)).ShouldBe("C");
    }

    /// <summary>Verifies an inner container with both horizontal and vertical bars - the shared
    /// bottom-right corner cell that the horizontal bar owns (see the scrollbar-corner fix) -
    /// still renders correctly once nested inside an outer container that itself scrolls
    /// vertically and has already been scrolled, so the inner block's own corner is translated
    /// away from the origin rather than sitting at a fixed screen position.</summary>
    [Fact]
    public void Render_WhenInnerHasBothBarsAndOuterIsScrolled_CornerCellTranslatesWithInnerBlock()
    {
        var top = new ProbeControl(new Size(1, 1)) { Content = "T".AsMemory() };
        var leaf = new ProbeControl(new Size(6, 6));
        var inner = new LayoutProbe
        {
            Width = Length.Cells(4),
            Height = Length.Cells(4),
            AutoScroll = true,
            ScrollBars = ScrollBars.Both
        };
        inner.Children.Add(leaf);
        var outer = new Stack
        {
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            Children = { top, inner }
        };

        new LayoutEngine().Layout(outer, new Size(5, 4));
        _ = outer.ScrollBy(0, 1);
        new LayoutEngine().Layout(outer, new Size(5, 4));
        using Frame frame = new(new Size(5, 4));

        outer.Render(frame.Canvas);

        // outer's own extent (1 + 4 = 5) less its own viewport (4) leaves exactly one line of
        // scroll capacity - after scrolling by 1, inner's own block occupies absolute rows
        // [0, 4) instead of [1, 5), with 'top' scrolled fully out of view.
        var innerAbsoluteBounds = inner.Bounds;
        innerAbsoluteBounds.Y.ShouldBe(0);

        // inner's own bottom-right corner, translated to absolute coordinates, hit-tests as the
        // horizontal bar - the documented shared-corner owner - not the vertical one, and not a
        // stray content cell.
        var corner = new Point(innerAbsoluteBounds.Right - 1, innerAbsoluteBounds.Bottom - 1);
        outer.HitTest(corner).ShouldBeOfType<ScrollBar>().Orientation.ShouldBe(Orientation.Horizontal);
    }
}
