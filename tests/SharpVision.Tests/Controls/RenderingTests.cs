// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;



/// <summary>Verifies clipped, ordered, grapheme-safe control rendering into semantic cells.</summary>
public sealed class RenderingTests
{
    /// <summary>Verifies parent clipping and later-child overwrite order.</summary>
    [Fact]
    public void Render_WhenChildrenOverlap_ClipsAndUsesCollectionZOrder()
    {
        ProbeContainer root = new() { Bounds = new Rect(0, 0, 5, 2) };
        ProbeControl first = new()
        {
            Bounds = new Rect(0, 0, 8, 1),
            Content = "ABCDEFGH".AsMemory(),
        };
        ProbeControl second = new()
        {
            Bounds = new Rect(2, 0, 1, 1),
            Content = "Z".AsMemory(),
        };
        root.Children.Add(first);
        root.Children.Add(second);
        using Frame frame = new(new Size(8, 2));

        root.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("A");
        FrameOracle.Get(frame, new Point(2, 0)).ShouldBe("Z");
        FrameOracle.Get(frame, new Point(4, 0)).ShouldBe("E");
        FrameOracle.Get(frame, new Point(5, 0)).ShouldBeEmpty();
        frame.Cursor.ShouldBe(default);
    }

    /// <summary>Verifies an explicit child policy can render outside the owner's bounds.</summary>
    [Fact]
    public void Render_WhenContainerDoesNotClipChildren_DrawsWithinAncestorCanvas()
    {
        ProbeContainer root = new()
        {
            Bounds = new Rect(0, 0, 1, 1),
            ClipChildren = false,
        };
        ProbeControl child = new()
        {
            Bounds = new Rect(1, 0, 1, 1),
            Content = "X".AsMemory(),
        };
        root.Children.Add(child);
        using Frame frame = new(new Size(2, 1));

        root.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(1, 0)).ShouldBe("X");
    }

    /// <summary>Verifies hit-test transparency leaves semantic drawing enabled.</summary>
    [Fact]
    public void Render_WhenControlIsHitTestTransparent_StillDrawsCells()
    {
        ProbeControl control = new()
        {
            Bounds = new Rect(0, 0, 1, 1),
            Content = "X".AsMemory(),
            IsHitTestVisible = false,
        };
        using Frame frame = new(new Size(1, 1));

        control.Render(frame.Canvas);

        FrameOracle.Get(frame, default).ShouldBe("X");
    }

    /// <summary>Verifies hidden and collapsed controls skip rendering entirely.</summary>
    [Fact]
    public void Render_WhenVisibilitySuppressesDrawing_LeavesCellsBlank()
    {
        ProbeContainer root = new() { Bounds = new Rect(0, 0, 4, 1) };
        ProbeControl hidden = new()
        {
            Bounds = new Rect(0, 0, 1, 1),
            Content = "H".AsMemory(),
            Visibility = Visibility.Hidden,
        };
        ProbeControl collapsed = new()
        {
            Bounds = new Rect(1, 0, 1, 1),
            Content = "C".AsMemory(),
            Visibility = Visibility.Collapsed,
        };
        root.Children.Add(hidden);
        root.Children.Add(collapsed);
        using Frame frame = new(new Size(4, 1));

        root.Render(frame.Canvas);

        hidden.RenderCalls.ShouldBe(0);
        collapsed.RenderCalls.ShouldBe(0);
        FrameOracle.Get(frame, default).ShouldBeEmpty();
        FrameOracle.Get(frame, new Point(1, 0)).ShouldBeEmpty();
    }

    /// <summary>Verifies arranged padding moves the semantic content origin.</summary>
    [Fact]
    public void Render_WhenControlHasPadding_DrawsInsideContentBox()
    {
        ProbeControl control = new()
        {
            Padding = new Thickness(1),
            Content = "A".AsMemory(),
        };
        new Engine().Layout(control, new Size(5, 3));
        using Frame frame = new(new Size(5, 3));

        control.Render(frame.Canvas);

        FrameOracle.Get(frame, default).ShouldBeEmpty();
        FrameOracle.Get(frame, new Point(1, 1)).ShouldBe("A");
    }

    /// <summary>Verifies combining, wide, and emoji clusters preserve cell ownership.</summary>
    [Fact]
    public void Render_WhenContentHasComplexGraphemes_PreservesLeadAndContinuations()
    {
        ProbeControl control = new()
        {
            Bounds = new Rect(0, 0, 8, 1),
            Content = "e\u0301界👩‍💻".AsMemory(),
        };
        using Frame frame = new(new Size(8, 1));

        control.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("e\u0301");
        FrameOracle.Get(frame, new Point(1, 0)).ShouldBe("界");
        frame.GetCell(new Point(2, 0)).IsContinuation.ShouldBeTrue();
        frame.GetCell(new Point(2, 0)).Lead.ShouldBe(new Point(1, 0));
        FrameOracle.Get(frame, new Point(3, 0)).ShouldBe("👩‍💻");
        frame.GetCell(new Point(4, 0)).IsContinuation.ShouldBeTrue();
        frame.GetCell(new Point(4, 0)).Lead.ShouldBe(new Point(3, 0));
    }

    /// <summary>Verifies resolved visual state reaches final cell style.</summary>
    [Fact]
    public void Render_WhenControlStateChanges_WritesResolvedStyle()
    {
        ControlStyle<ProbeControl> style = ThemeTestSupport.OverlayStyle<ProbeControl>(
            (State.Normal, new ThemeOverlay(foreground: Color.Indexed(2))),
            (State.Hovered, new ThemeOverlay(attributes: Attributes.Underline)),
            (State.Pressed, new ThemeOverlay(foreground: Color.Indexed(5))));
        ProbeControl control = new()
        {
            Bounds = new Rect(0, 0, 1, 1),
            Content = "A".AsMemory(),
            Style = style,
        };
        control.SetHovered(true);
        control.SetPressed(true);
        using Frame frame = new(new Size(1, 1));

        control.Render(frame.Canvas);

        CellInfo cell = frame.GetCell(default);
        cell.Style.Foreground.ShouldBe(Color.Indexed(5));
        cell.Style.Attributes.ShouldBe(Attributes.Underline);
    }

    /// <summary>Verifies render-time invalidation remains pending without recursive rendering.</summary>
    [Fact]
    public void Render_WhenCoreInvalidates_LeavesNextFramePending()
    {
        ProbeControl control = new()
        {
            Bounds = new Rect(0, 0, 1, 1),
            Content = "A".AsMemory(),
            Rendering = current => current.SetHovered(true),
        };
        using Frame frame = new(new Size(1, 1));
        control.Clear(Invalidation.All);
        control.Invalidate(Invalidation.Render);

        control.Render(frame.Canvas);

        control.RenderCalls.ShouldBe(1);
        control.Pending.ShouldBe(Invalidation.Render);
        control.Rendering = null;
        frame.Clear();
        control.Render(frame.Canvas);
        control.Pending.ShouldBe(Invalidation.None);
    }

    /// <summary>Verifies a failed render restores dirtiness before preserving the exception.</summary>
    [Fact]
    public void Render_WhenCoreThrows_RestoresRenderInvalidation()
    {
        InvalidOperationException failure = new("render");
        ProbeControl control = new()
        {
            Bounds = new Rect(0, 0, 1, 1),
            Rendering = _ => throw failure,
        };
        using Frame frame = new(new Size(1, 1));
        control.Clear(Invalidation.All);
        control.Invalidate(Invalidation.Render);

        Should.Throw<InvalidOperationException>(() => control.Render(frame.Canvas))
            .ShouldBeSameAs(failure);

        control.Pending.ShouldBe(Invalidation.Render);
    }

    /// <summary>Verifies zero and tiny bounds are safe and cannot write outside their clip.</summary>
    [Fact]
    public void Render_WhenBoundsAreZeroOrTiny_DoesNotEscapeClip()
    {
        ProbeContainer root = new() { Bounds = new Rect(0, 0, 1, 1) };
        ProbeControl zero = new()
        {
            Bounds = new Rect(0, 0, 0, 0),
            Content = "X".AsMemory(),
        };
        ProbeControl outside = new()
        {
            Bounds = new Rect(1, 0, 2, 1),
            Content = "YZ".AsMemory(),
        };
        root.Children.Add(zero);
        root.Children.Add(outside);
        using Frame frame = new(new Size(2, 1));

        root.Render(frame.Canvas);

        FrameOracle.Get(frame, default).ShouldBeEmpty();
        FrameOracle.Get(frame, new Point(1, 0)).ShouldBeEmpty();
    }
}
