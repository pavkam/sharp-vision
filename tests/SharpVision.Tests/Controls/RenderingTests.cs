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
        var root = new ProbeContainer() { Bounds = new Rect(0, 0, 5, 2) };
        var first = new ProbeControl()
        {
            Bounds = new Rect(0, 0, 8, 1),
            Content = "ABCDEFGH".AsMemory(),
        };
        var second = new ProbeControl()
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
        var root = new ProbeContainer()
        {
            Bounds = new Rect(0, 0, 1, 1),
            ClipChildren = false,
        };
        var child = new ProbeControl()
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
        var control = new ProbeControl()
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
        var root = new ProbeContainer() { Bounds = new Rect(0, 0, 4, 1) };
        var hidden = new ProbeControl()
        {
            Bounds = new Rect(0, 0, 1, 1),
            Content = "H".AsMemory(),
            Visibility = Visibility.Hidden,
        };
        var collapsed = new ProbeControl()
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
        var control = new ProbeControl()
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
        var control = new ProbeControl()
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

    /// <summary>Verifies a failed render restores dirtiness before preserving the exception.</summary>
    [Fact]
    public void Render_WhenCoreThrows_RestoresRenderInvalidation()
    {
        var failure = new InvalidOperationException("render");
        var control = new ProbeControl()
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
        var root = new ProbeContainer() { Bounds = new Rect(0, 0, 1, 1) };
        var zero = new ProbeControl()
        {
            Bounds = new Rect(0, 0, 0, 0),
            Content = "X".AsMemory(),
        };
        var outside = new ProbeControl()
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
