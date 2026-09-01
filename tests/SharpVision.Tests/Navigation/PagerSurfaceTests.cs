// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Navigation;

/// <summary>Verifies mounted Pager rendering and input against committed target snapshots.</summary>
public sealed class PagerSurfaceTests
{
    /// <summary>Verifies an unbounded middle-page layout emits navigation, endpoint numbers, gaps, and window pages in source order.</summary>
    [Fact]
    public void Render_WhenMiddlePageHasRoom_WritesCompleteIdealSequence()
    {
        var pager = new Pager
        {
            PageCount = 10,
            PageIndex = 4,
            MaximumVisiblePages = 3,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        new LayoutEngine().Layout(pager, new Size(29, 1));
        using Frame frame = new(new Size(29, 1));

        pager.Render(frame.Canvas);

        Row(frame, 29).TrimEnd().ShouldBe("« ‹ 1 … 3 4 5 6 … 10 › »");
    }

    /// <summary>Verifies finite retention keeps the current number before endpoint and nearest-window candidates.</summary>
    [Fact]
    public void Render_WhenWidthIsNarrow_RetainsWholeTargetsByPriority()
    {
        var pager = new Pager
        {
            PageCount = 10,
            PageIndex = 4,
            MaximumVisiblePages = 3,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        new LayoutEngine().Layout(pager, new Size(5, 1));
        using Frame frame = new(new Size(5, 1));

        pager.Render(frame.Canvas);

        Row(frame, 5).ShouldBe("1 4 5");
        pager.LayoutSnapshot.Targets.Select(static target => target.PageIndex).ShouldBe([0, 3, 4]);
    }

    /// <summary>Verifies an unfittable current number produces no partial cells or pointer target.</summary>
    [Fact]
    public void Render_WhenCurrentNumberDoesNotFit_WritesNothing()
    {
        var pager = new Pager
        {
            PageCount = 100,
            PageIndex = 99,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        new LayoutEngine().Layout(pager, new Size(2, 1));
        using Frame frame = new(new Size(2, 1));

        pager.Render(frame.Canvas);

        Row(frame, 2).ShouldBe("  ");
        pager.LayoutSnapshot.Targets.ShouldBeEmpty();
    }

    /// <summary>Verifies primary release activates the captured numbered-target identity.</summary>
    [Fact]
    public async Task Pointer_WhenNumberIsPressedAndReleased_ChangesPageAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var pager = new Pager { PageCount = 5, HorizontalAlignment = HorizontalAlignment.Stretch };
            pager.Attach(dispatcher);
            new LayoutEngine().Layout(pager, new Size(24, 1));
            using FocusManager focus = new(pager);
            using PointerManager pointer = new(pager);
            PageChangedEventArgs? change = null;
            pager.PageChanged += (_, eventArgs) => change = eventArgs;
            var target = pager.LayoutSnapshot.Targets.Single(item =>
                item.Kind == PagerTargetKind.Number && item.PageIndex == 3);

            _ = pointer.Dispatch(PointerAt(target.Bounds, PointerAction.Press));

            pointer.Captured.ShouldBeSameAs(pager);
            pager.IsPressed.ShouldBeTrue();

            _ = pointer.Dispatch(PointerAt(target.Bounds, PointerAction.Release));

            pointer.Captured.ShouldBeNull();
            pager.IsPressed.ShouldBeFalse();
            pager.PageIndex.ShouldBe(3);
            change.ShouldNotBeNull().Cause.ShouldBe(ActivationCause.Pointer);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a newer layout cancels capture so one physical cell cannot be reinterpreted.</summary>
    [Fact]
    public async Task Pointer_WhenLayoutChangesBeforeRelease_DoesNotActivateStaleTargetAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var pager = new Pager { PageCount = 20, PageIndex = 10, HorizontalAlignment = HorizontalAlignment.Stretch };
            pager.Attach(dispatcher);
            new LayoutEngine().Layout(pager, new Size(30, 1));
            using FocusManager focus = new(pager);
            using PointerManager pointer = new(pager);
            var target = pager.LayoutSnapshot.Targets.First(item =>
                item.Kind == PagerTargetKind.Number && item.PageIndex != pager.PageIndex);
            _ = pointer.Dispatch(PointerAt(target.Bounds, PointerAction.Press));

            new LayoutEngine().Layout(pager, new Size(5, 1));
            _ = pointer.Dispatch(PointerAt(target.Bounds, PointerAction.Release));

            pager.PageIndex.ShouldBe(10);
            pointer.Captured.ShouldBeNull();
            pager.IsPressed.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    private static Pointer PointerAt(Rect bounds, PointerAction action) => new(
        new Point(bounds.X, bounds.Y),
        pixels: null,
        Buttons.Primary,
        action,
        wheelX: 0,
        wheelY: 0,
        Modifiers.None,
        isMotion: false,
        isCellPositionInferred: false);

    private static string Row(Frame frame, int width)
    {
        var text = new StringBuilder(width);

        for (var x = 0; x < width; x++)
        {
            var cell = FrameOracle.Get(frame, new Point(x, 0));
            _ = text.Append(cell.Length == 0 ? " " : cell);
        }

        return text.ToString();
    }
}
