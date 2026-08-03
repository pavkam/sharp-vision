// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

using GraphicsImage = Terminal.Graphics.ImageSource;

/// <summary>
/// Verifies the narrow, maintainer-approved render-clean subtree reuse cut (see #26) and its first
/// #235 extension: a leaf control that is render-clean, image-free, and popup-free copies its
/// previous frame's cells instead of re-executing its paint sequence, but only when no layout ran
/// since the copied frame (<see cref="Canvas.HasPreviousFrame"/>). A visible shadow now
/// participates too, but only when its own paint is a full destination overwrite that cannot
/// depend on stale prior content - BlockGlyph mode with an opaque resolved background. Composite
/// (which never replaces the underlying grapheme, regardless of background opacity) and
/// FractionalBlock (which always blends) stay excluded. Popup-overlapped and image-bearing
/// subtrees also stay excluded and remain tracked in #235.
/// </summary>
public sealed class RenderCleanSubtreeReuseTests
{
    /// <summary>Verifies a clean sibling's render extension point is skipped while a dirty
    /// sibling still renders, and both produce correct final cell content.</summary>
    [Fact]
    public async Task Render_WhenSiblingIsDirty_SkipsCleanLeafRenderCallsAsync()
    {
        var dirty = new ProbeControl(new Size(4, 1)) { Content = "AAAA".AsMemory() };
        var clean = new ProbeControl(new Size(4, 1)) { Content = "BBBB".AsMemory() };
        var stack = new Stack { Children = { dirty, clean } };
        var size = new Size(4, 2);
        new LayoutEngine().Layout(stack, size);
        using var renderer = new Renderer();
        var transport = new ConsoleApplicationTransport();
        var profile = TerminalProfile.CreateAnsi(Capabilities.Conservative);
        using var first = new Frame(size);
        stack.Render(first.Canvas);
        _ = await renderer.RenderAsync(first, transport, profile, TestContext.Current.CancellationToken);
        dirty.RenderCalls.ShouldBe(1);
        clean.RenderCalls.ShouldBe(1);

        dirty.Invalidate(Invalidation.Render);
        using var second = new Frame(size);
        var attached = renderer.AttachCommittedFrame(second);

        stack.Render(second.Canvas);

        attached.ShouldBeTrue();
        dirty.RenderCalls.ShouldBe(2);
        clean.RenderCalls.ShouldBe(1);
        Row(second, 0).ShouldBe("AAAA");
        Row(second, 1).ShouldBe("BBBB");
    }

    /// <summary>Verifies every clean leaf still runs its complete paint sequence when no previous
    /// frame is attached, matching what happens after a layout pass (see Application.StartRender,
    /// which never attaches when layout ran since the last render).</summary>
    [Fact]
    public async Task Render_WhenNoPreviousFrameIsAttached_RendersEveryCleanLeafAsync()
    {
        var first = new ProbeControl(new Size(2, 1)) { Content = "AA".AsMemory() };
        var second = new ProbeControl(new Size(2, 1)) { Content = "BB".AsMemory() };
        var stack = new Stack { Children = { first, second } };
        var size = new Size(2, 2);
        new LayoutEngine().Layout(stack, size);
        using var renderer = new Renderer();
        var transport = new ConsoleApplicationTransport();
        var profile = TerminalProfile.CreateAnsi(Capabilities.Conservative);
        using var firstFrame = new Frame(size);
        stack.Render(firstFrame.Canvas);
        _ = await renderer.RenderAsync(firstFrame, transport, profile, TestContext.Current.CancellationToken);

        using var secondFrame = new Frame(size);
        secondFrame.Canvas.HasPreviousFrame.ShouldBeFalse();
        stack.Render(secondFrame.Canvas);

        first.RenderCalls.ShouldBe(2);
        second.RenderCalls.ShouldBe(2);
    }

    /// <summary>Verifies a render-clean leaf casting the default Composite-mode, transparent-
    /// background shadow never takes the copy path. Composite mode's own contract is to preserve
    /// the underlying grapheme and replace only its style (<see cref="Canvas.ApplyStyle"/> calls
    /// only <c>TrySetOwnerStyle</c>), so a copied Composite shadow cell would always carry forward
    /// whatever character was underneath in the copied frame rather than this frame's - a copy is
    /// never provably identical to a fresh paint for this mode, regardless of background opacity
    /// (see #235).</summary>
    [Fact]
    public async Task Render_WhenLeafHasVisibleShadow_NeverSkipsRenderAsync()
    {
        var control = new ProbeControl(new Size(2, 1)) { Shadow = AppearanceTestValues.Shadow(visible: true) };
        var size = new Size(4, 3);
        new LayoutEngine().Layout(control, size);
        using var renderer = new Renderer();
        var transport = new ConsoleApplicationTransport();
        var profile = TerminalProfile.CreateAnsi(Capabilities.Conservative);
        using var first = new Frame(size);
        control.Render(first.Canvas);
        _ = await renderer.RenderAsync(first, transport, profile, TestContext.Current.CancellationToken);
        control.RenderCalls.ShouldBe(1);

        using var second = new Frame(size);
        _ = renderer.AttachCommittedFrame(second);
        control.Render(second.Canvas);

        control.RenderCalls.ShouldBe(2);
    }

    /// <summary>Verifies a render-clean leaf casting a Composite-mode shadow never takes the copy
    /// path even when its resolved background is opaque - opacity only changes whether the
    /// background channel blends, not whether the grapheme does, and Composite never replaces the
    /// grapheme regardless (see #235).</summary>
    [Fact]
    public async Task Render_WhenLeafHasVisibleCompositeShadowWithOpaqueBackground_NeverSkipsRenderAsync()
    {
        var control = new ProbeControl(new Size(2, 1))
        {
            Shadow = AppearanceTestValues.Shadow(
                visible: true,
                mode: ShadowMode.Composite,
                offset: new Point(1, 0),
                background: Color.Rgb(20, 20, 20))
        };
        var size = new Size(4, 3);
        new LayoutEngine().Layout(control, size);
        using var renderer = new Renderer();
        var transport = new ConsoleApplicationTransport();
        var profile = TerminalProfile.CreateAnsi(Capabilities.Conservative);
        using var first = new Frame(size);
        control.Render(first.Canvas);
        _ = await renderer.RenderAsync(first, transport, profile, TestContext.Current.CancellationToken);
        control.RenderCalls.ShouldBe(1);

        using var second = new Frame(size);
        _ = renderer.AttachCommittedFrame(second);
        control.Render(second.Canvas);

        control.RenderCalls.ShouldBe(2);
    }

    /// <summary>Verifies a render-clean leaf casting a FractionalBlock shadow never takes the copy
    /// path regardless of its configured background - <c>DrawFractionalShadow</c> hardcodes
    /// <see cref="BackgroundMode.Transparent"/> unconditionally, so this mode always blends with
    /// the destination (see #235).</summary>
    [Fact]
    public async Task Render_WhenLeafHasVisibleFractionalBlockShadow_NeverSkipsRenderAsync()
    {
        var control = new ProbeControl(new Size(2, 1))
        {
            Shadow = AppearanceTestValues.Shadow(
                visible: true,
                mode: ShadowMode.FractionalBlock,
                offset: new Point(0, 1),
                background: Color.Rgb(20, 20, 20))
        };
        var size = new Size(4, 3);
        new LayoutEngine().Layout(control, size);
        using var renderer = new Renderer();
        var transport = new ConsoleApplicationTransport();
        var profile = TerminalProfile.CreateAnsi(Capabilities.Conservative);
        using var first = new Frame(size);
        control.Render(first.Canvas);
        _ = await renderer.RenderAsync(first, transport, profile, TestContext.Current.CancellationToken);
        control.RenderCalls.ShouldBe(1);

        using var second = new Frame(size);
        _ = renderer.AttachCommittedFrame(second);
        control.Render(second.Canvas);

        control.RenderCalls.ShouldBe(2);
    }

    /// <summary>Verifies a render-clean leaf casting a BlockGlyph shadow with an opaque resolved
    /// background does take the copy path - <c>DrawRune</c> with an opaque background replaces
    /// grapheme, style, and background together, so the copied cells are provably identical to a
    /// fresh paint (see #235).</summary>
    [Fact]
    public async Task Render_WhenLeafHasVisibleBlockGlyphShadowWithOpaqueBackground_SkipsRenderAsync()
    {
        var control = new ProbeControl(new Size(2, 1))
        {
            Shadow = AppearanceTestValues.Shadow(
                visible: true,
                mode: ShadowMode.BlockGlyph,
                offset: new Point(1, 0),
                background: Color.Rgb(20, 20, 20))
        };
        var size = new Size(4, 3);
        new LayoutEngine().Layout(control, size);
        using var renderer = new Renderer();
        var transport = new ConsoleApplicationTransport();
        var profile = TerminalProfile.CreateAnsi(Capabilities.Conservative);
        using var first = new Frame(size);
        control.Render(first.Canvas);
        _ = await renderer.RenderAsync(first, transport, profile, TestContext.Current.CancellationToken);
        control.RenderCalls.ShouldBe(1);

        using var second = new Frame(size);
        _ = renderer.AttachCommittedFrame(second);
        control.Render(second.Canvas);

        control.RenderCalls.ShouldBe(1);
    }

    /// <summary>Verifies a BlockGlyph, opaque-background shadow whose footprint overlaps a
    /// changing sibling still produces cell content identical to a fully fresh render - proving
    /// the reuse extension is safe even when the copied region is not exclusively owned by the
    /// reused control, because paint order (not paint source) determines the final cell and the
    /// shadow-casting control's own copied output never changes frame to frame (see #235).</summary>
    [Fact]
    public async Task Render_WhenShadowOverlapsChangingSibling_MatchesFullRenderEveryFrameAsync()
    {
        var caster = new ProbeControl(new Size(2, 1))
        {
            Content = "AA".AsMemory(),
            Shadow = AppearanceTestValues.Shadow(
                visible: true,
                mode: ShadowMode.BlockGlyph,
                offset: new Point(1, 0),
                background: Color.Rgb(20, 20, 20))
        };
        var sibling = new ProbeControl(new Size(1, 1)) { Content = "X".AsMemory() };
        var overlay = new Overlay { Children = { caster, sibling } };
        Overlay.SetLeft(sibling, Length.Cells(2));
        var size = new Size(3, 1);
        new LayoutEngine().Layout(overlay, size);
        using var renderer = new Renderer();
        var transport = new ConsoleApplicationTransport();
        var profile = TerminalProfile.CreateAnsi(Capabilities.Conservative);
        using var warm = new Frame(size);
        overlay.Render(warm.Canvas);
        _ = await renderer.RenderAsync(warm, transport, profile, TestContext.Current.CancellationToken);
        caster.RenderCalls.ShouldBe(1);

        sibling.Content = "Y".AsMemory();
        sibling.Invalidate(Invalidation.Render);

        using var reused = new Frame(size);
        _ = renderer.AttachCommittedFrame(reused);
        overlay.Render(reused.Canvas);

        caster.RenderCalls.ShouldBe(1);
        sibling.RenderCalls.ShouldBe(2);

        // No previous frame is attached, so this independent render of the exact same current
        // state always takes the complete paint path for every leaf - the ground truth the
        // optimized render above must match cell-for-cell, including whichever leaf's paint owns
        // the contested cell where the shadow's footprint and the sibling's bounds overlap.
        using var reference = new Frame(size);
        overlay.Render(reference.Canvas);

        Row(reused, 0).ShouldBe(Row(reference, 0));
    }

    /// <summary>Verifies the harder ordering of the previous test - the shadow-casting control
    /// paints AFTER the sibling it overlaps, so its (possibly copied) shadow cells are the last
    /// write at the contested position. Still matches a fully fresh render cell-for-cell, because
    /// the copied shadow bytes are provably identical to what caster would freshly paint - they
    /// depend only on caster's own unchanged appearance, never on what the sibling drew
    /// underneath (see #235).</summary>
    [Fact]
    public async Task Render_WhenShadowPaintsOverAChangingSibling_MatchesFullRenderEveryFrameAsync()
    {
        var sibling = new ProbeControl(new Size(1, 1)) { Content = "X".AsMemory() };
        var caster = new ProbeControl(new Size(2, 1))
        {
            Content = "AA".AsMemory(),
            Shadow = AppearanceTestValues.Shadow(
                visible: true,
                mode: ShadowMode.BlockGlyph,
                offset: new Point(1, 0),
                background: Color.Rgb(20, 20, 20))
        };
        var overlay = new Overlay { Children = { sibling, caster } };
        Overlay.SetLeft(sibling, Length.Cells(2));
        var size = new Size(3, 1);
        new LayoutEngine().Layout(overlay, size);
        using var renderer = new Renderer();
        var transport = new ConsoleApplicationTransport();
        var profile = TerminalProfile.CreateAnsi(Capabilities.Conservative);
        using var warm = new Frame(size);
        overlay.Render(warm.Canvas);
        _ = await renderer.RenderAsync(warm, transport, profile, TestContext.Current.CancellationToken);
        caster.RenderCalls.ShouldBe(1);

        sibling.Content = "Y".AsMemory();
        sibling.Invalidate(Invalidation.Render);

        using var reused = new Frame(size);
        _ = renderer.AttachCommittedFrame(reused);
        overlay.Render(reused.Canvas);

        caster.RenderCalls.ShouldBe(1);
        sibling.RenderCalls.ShouldBe(2);

        using var reference = new Frame(size);
        overlay.Render(reference.Canvas);

        Row(reused, 0).ShouldBe(Row(reference, 0));
    }

    /// <summary>Verifies a render-clean leaf that owns a control of its own (a context menu, even
    /// while closed) never takes the copy path - <c>OwnedControlCount</c> covers both the normal
    /// and popup layers, so this is the leaf's own conservative "popup-free" requirement.</summary>
    [Fact]
    public async Task Render_WhenLeafOwnsAControl_NeverSkipsRenderAsync()
    {
        var control = new ProbeControl(new Size(2, 1)) { ContextMenu = new ContextMenu() };
        var size = new Size(4, 3);
        new LayoutEngine().Layout(control, size);
        using var renderer = new Renderer();
        var transport = new ConsoleApplicationTransport();
        var profile = TerminalProfile.CreateAnsi(Capabilities.Conservative);
        using var first = new Frame(size);
        control.Render(first.Canvas);
        _ = await renderer.RenderAsync(first, transport, profile, TestContext.Current.CancellationToken);
        control.RenderCalls.ShouldBe(1);

        using var second = new Frame(size);
        _ = renderer.AttachCommittedFrame(second);
        control.Render(second.Canvas);

        control.RenderCalls.ShouldBe(2);
    }

    /// <summary>Verifies a render-clean leaf whose resolved background is transparent never takes
    /// the copy path. A transparent underlay never authors its own uncovered cells - they hold
    /// whatever the parent painted underneath, which the copy path would resurrect as stale content
    /// from the frame it copies rather than the parent's current-frame paint (see #239).</summary>
    [Fact]
    public async Task Render_WhenLeafBackgroundIsTransparent_NeverSkipsRenderAsync()
    {
        var control = new ProbeControl(new Size(2, 1)) { Content = "AA".AsMemory() };
        var defaultFace = control.Face;
        control.Face = new Face(
            defaultFace.Foreground,
            Color.Transparent,
            defaultFace.Attributes,
            defaultFace.Underline,
            defaultFace.UnderlineColor);
        var size = new Size(4, 3);
        new LayoutEngine().Layout(control, size);
        using var renderer = new Renderer();
        var transport = new ConsoleApplicationTransport();
        var profile = TerminalProfile.CreateAnsi(Capabilities.Conservative);
        using var first = new Frame(size);
        control.Render(first.Canvas);
        _ = await renderer.RenderAsync(first, transport, profile, TestContext.Current.CancellationToken);
        control.RenderCalls.ShouldBe(1);

        using var second = new Frame(size);
        _ = renderer.AttachCommittedFrame(second);
        control.Render(second.Canvas);

        control.RenderCalls.ShouldBe(2);
    }

    /// <summary>Verifies a render-clean Image with an assigned source always re-records its
    /// semantic placement instead of silently dropping it through the copy path - copying cells
    /// never replays <see cref="Canvas.DrawImage"/> (see #26).</summary>
    [Fact]
    public async Task Render_WhenImageHasSource_AlwaysRecordsPlacementAsync()
    {
        var image = new Image
        {
            Source = GraphicsImage.FromRgba(new Size(1, 1), new byte[4]),
            Width = Length.Cells(1),
            Height = Length.Cells(1)
        };
        var size = new Size(1, 1);
        new LayoutEngine().Layout(image, size);
        using var renderer = new Renderer();
        var transport = new ConsoleApplicationTransport();
        var profile = TerminalProfile.CreateAnsi(Capabilities.Conservative);
        using var first = new Frame(size);
        image.Render(first.Canvas);
        first.PlacementCount.ShouldBe(1);
        _ = await renderer.RenderAsync(first, transport, profile, TestContext.Current.CancellationToken);

        using var second = new Frame(size);
        _ = renderer.AttachCommittedFrame(second);
        image.Render(second.Canvas);

        second.PlacementCount.ShouldBe(1);
        second.GetPlacement(0).Image.ShouldBeSameAs(image.Source);
    }

    /// <summary>Verifies a fixed sequence of frames mixing clean and dirty leaves - none dirty,
    /// all dirty, and various subsets - always produces cell content identical to a fully fresh
    /// render of the same state, proving the copy path never diverges from the full paint path.</summary>
    [Fact]
    public async Task Render_WhenDirtyLeavesVaryAcrossFrames_MatchesFullRenderEveryFrameAsync()
    {
        var leaves = Enumerable.Range(0, 5)
            .Select(index => new ProbeControl(new Size(4, 1)) { Content = $"L{index}--".AsMemory() })
            .ToArray();
        var stack = new Stack { Children = { leaves[0], leaves[1], leaves[2], leaves[3], leaves[4] } };
        var size = new Size(4, 5);
        new LayoutEngine().Layout(stack, size);
        using var renderer = new Renderer();
        var transport = new ConsoleApplicationTransport();
        var profile = TerminalProfile.CreateAnsi(Capabilities.Conservative);
        using var warm = new Frame(size);
        stack.Render(warm.Canvas);
        _ = await renderer.RenderAsync(warm, transport, profile, TestContext.Current.CancellationToken);

        int[][] dirtyIndexSets =
        [
            [],
            [0, 1, 2, 3, 4],
            [2],
            [0, 4],
            [],
            [1, 2, 3],
            []
        ];

        foreach (var dirtyIndices in dirtyIndexSets)
        {
            foreach (var index in dirtyIndices)
            {
                leaves[index].Content = $"U{index}--".AsMemory();
                leaves[index].Invalidate(Invalidation.Render);
            }

            using var reused = new Frame(size);
            _ = renderer.AttachCommittedFrame(reused);
            stack.Render(reused.Canvas);

            // No previous frame is attached, so this independent render of the exact same
            // current state always takes the complete paint path for every leaf - the ground
            // truth the optimized render above must match cell-for-cell.
            using var reference = new Frame(size);
            stack.Render(reference.Canvas);

            for (var row = 0; row < size.Height; row++)
            {
                Row(reused, row).ShouldBe(Row(reference, row));
            }

            _ = await renderer.RenderAsync(reused, transport, profile, TestContext.Current.CancellationToken);
        }
    }

    private static string Row(Frame frame, int row)
    {
        var value = new StringBuilder();

        for (var column = 0; column < frame.Size.Width; column++)
        {
            _ = value.Append(FrameOracle.Get(frame, new Point(column, row)));
        }

        return value.ToString();
    }
}
