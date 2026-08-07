// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

using GraphicsImage = Terminal.Graphics.ImageSource;

/// <summary>
/// Verifies the narrow, maintainer-approved render-clean subtree reuse cut and its
/// extensions: a leaf control that is render-clean and owns no popup of its own copies its
/// previous frame's cells instead of re-executing its paint sequence, but only when no layout ran
/// since the copied frame (<see cref="TerminalCanvas.HasPreviousFrame"/>). A visible shadow
/// participates too, but only when its own paint is a full destination overwrite that cannot
/// depend on stale prior content - BlockGlyph mode with an opaque resolved background. Composite
/// (which never replaces the underlying grapheme, regardless of background opacity) and
/// FractionalBlock (which always blends) stay excluded.
///
/// Image-bearing subtrees participate too, in the third slice of this extension: a plain cell copy restores the
/// fallback shade and alternate-text cells correctly, but never replays
/// <c>TerminalCanvas.DrawImage</c>'s out-of-band semantic placement, so
/// <c>ControlBase.OnReuseCleanRender</c> re-asserts it by reading <see cref="Image"/>'s own
/// current <c>Source</c>/<c>Stretch</c>/<c>ContentBounds</c> at the exact traversal position a
/// fresh paint would have run.
///
/// Being overlapped or bordered by a popup that belongs to a DIFFERENT control needs no exclusion
/// at all: the popup layer always repaints unconditionally after ordinary content on every frame
/// (steady state), and any frame where a popup's footprint could have changed is, by construction,
/// a frame where <see cref="TerminalCanvas.HasPreviousFrame"/> is false for every control application-wide
/// (open/close/move all route through <c>InvalidationImpact.Measure</c>, which forces a full
/// non-reuse render - see the correctness note above <c>Control.CanReuseCleanRender</c>). The
/// adversarial popup cases below lock that invariant in.
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
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
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
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
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
    /// the underlying grapheme and replace only its style (<see cref="TerminalCanvas.ApplyStyle"/> calls
    /// only <c>TrySetOwnerStyle</c>), so a copied Composite shadow cell would always carry forward
    /// whatever character was underneath in the copied frame rather than this frame's - a copy is
    /// never provably identical to a fresh paint for this mode, regardless of background opacity.</summary>
    [Fact]
    public async Task Render_WhenLeafHasVisibleShadow_NeverSkipsRenderAsync()
    {
        var control = new ProbeControl(new Size(2, 1)) { Shadow = AppearanceTestValues.Shadow(visible: true) };
        var size = new Size(4, 3);
        new LayoutEngine().Layout(control, size);
        using var renderer = new Renderer();
        var transport = new ConsoleApplicationTransport();
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
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
    /// grapheme regardless.</summary>
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
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
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
    /// the destination.</summary>
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
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
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
    /// fresh paint.</summary>
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
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
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
    /// shadow-casting control's own copied output never changes frame to frame.</summary>
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
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
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
    /// underneath.</summary>
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
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
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
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
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
    /// from the frame it copies rather than the parent's current-frame paint.</summary>
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
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
        using var first = new Frame(size);
        control.Render(first.Canvas);
        _ = await renderer.RenderAsync(first, transport, profile, TestContext.Current.CancellationToken);
        control.RenderCalls.ShouldBe(1);

        using var second = new Frame(size);
        _ = renderer.AttachCommittedFrame(second);
        control.Render(second.Canvas);

        control.RenderCalls.ShouldBe(2);
    }

    /// <summary>Verifies a render-clean Image with an assigned source still records its semantic
    /// placement through the copy path: copying cells alone cannot replay
    /// <see cref="TerminalCanvas.DrawImage"/>, so <c>Image.OnReuseCleanRender</c> re-asserts an
    /// identical placement instead of silently dropping it.</summary>
    [Fact]
    public async Task Render_WhenImageIsRenderClean_PreservesPlacementThroughTheCopyPathAsync()
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
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
        using var first = new Frame(size);
        image.Render(first.Canvas);
        first.PlacementCount.ShouldBe(1);
        var firstPlacement = first.GetPlacement(0);
        _ = await renderer.RenderAsync(first, transport, profile, TestContext.Current.CancellationToken);

        using var second = new Frame(size);
        _ = renderer.AttachCommittedFrame(second);
        second.Canvas.HasPreviousFrame.ShouldBeTrue();
        image.Render(second.Canvas);

        second.PlacementCount.ShouldBe(1);
        var secondPlacement = second.GetPlacement(0);
        secondPlacement.Image.ShouldBeSameAs(image.Source);
        secondPlacement.Source.ShouldBe(firstPlacement.Source);
        secondPlacement.Destination.ShouldBe(firstPlacement.Destination);
        secondPlacement.Mode.ShouldBe(firstPlacement.Mode);
    }

    /// <summary>
    /// Verifies a render-clean Image actually takes the copy path rather than merely producing
    /// output consistent with either path. <see cref="Image"/> is sealed, so it cannot carry a
    /// <see cref="ProbeControl.RenderCalls"/>-style counter the way every other exclusion
    /// dimension in this file is proven; instead, this poisons the committed frame's fallback-cell
    /// content directly (bypassing <see cref="Image"/> entirely) after it paints and before the
    /// frame commits, then renders again with no further mutation. A full fresh paint would
    /// overwrite the poison with the recomputed fallback glyph; only <c>CopyFromPrevious</c>
    /// reproduces it verbatim.
    /// </summary>
    [Fact]
    public async Task Render_WhenImageIsRenderClean_ReallyTakesTheCopyPathAsync()
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
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
        using var first = new Frame(size);
        image.Render(first.Canvas);
        var freshFallbackGlyph = Row(first, 0);
        freshFallbackGlyph.ShouldNotBe("Z", "the poison marker must differ from a genuine fresh paint");
        _ = first.Canvas.Draw("Z", new Point(0, 0));
        _ = await renderer.RenderAsync(first, transport, profile, TestContext.Current.CancellationToken);

        using var second = new Frame(size);
        _ = renderer.AttachCommittedFrame(second);
        image.Render(second.Canvas);

        Row(second, 0).ShouldBe("Z");
    }

    /// <summary>Verifies a Source change invalidates render and produces a fresh, updated
    /// placement instead of one carried forward through reuse - a regression guard that removing
    /// Image's unconditional full-render requirement did not weaken ordinary invalidation.</summary>
    [Fact]
    public async Task Render_WhenImageSourceChangesBetweenFrames_RecordsTheNewPlacementAsync()
    {
        var firstSource = GraphicsImage.FromRgba(new Size(1, 1), [1, 2, 3, 4]);
        var secondSource = GraphicsImage.FromRgba(new Size(1, 1), [5, 6, 7, 8]);
        var image = new Image
        {
            Source = firstSource,
            Width = Length.Cells(1),
            Height = Length.Cells(1)
        };
        var size = new Size(1, 1);
        new LayoutEngine().Layout(image, size);
        using var renderer = new Renderer();
        var transport = new ConsoleApplicationTransport();
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
        using var first = new Frame(size);
        image.Render(first.Canvas);
        _ = await renderer.RenderAsync(first, transport, profile, TestContext.Current.CancellationToken);

        image.Source = secondSource;
        using var second = new Frame(size);
        _ = renderer.AttachCommittedFrame(second);
        image.Render(second.Canvas);

        second.PlacementCount.ShouldBe(1);
        second.GetPlacement(0).Image.ShouldBeSameAs(secondSource);
    }

    /// <summary>Verifies a fixed sequence of frames mixing an image-bearing leaf with dirty and
    /// clean plain siblings always produces cell content AND placement snapshots identical to a
    /// fully fresh render of the same state, proving the newly enabled image copy path never
    /// diverges from the full paint path.</summary>
    [Fact]
    public async Task Render_WhenImageLeafIsMixedWithChangingSiblings_MatchesFullRenderEveryFrameAsync()
    {
        var image = new Image
        {
            Source = GraphicsImage.FromRgba(new Size(1, 1), new byte[4]),
            Width = Length.Cells(2),
            Height = Length.Cells(1)
        };
        var before = new ProbeControl(new Size(4, 1)) { Content = "before".AsMemory() };
        var after = new ProbeControl(new Size(4, 1)) { Content = "after-".AsMemory() };
        var stack = new Stack { Children = { before, image, after } };
        var size = new Size(4, 3);
        new LayoutEngine().Layout(stack, size);
        using var renderer = new Renderer();
        var transport = new ConsoleApplicationTransport();
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
        using var warm = new Frame(size);
        stack.Render(warm.Canvas);
        _ = await renderer.RenderAsync(warm, transport, profile, TestContext.Current.CancellationToken);

        bool[][] dirtySequence =
        [
            [false, false], // neither sibling dirty: image is the only render-clean leaf either way
            [true, false],
            [false, true],
            [true, true]
        ];

        foreach (var dirty in dirtySequence)
        {
            if (dirty[0])
            {
                before.Invalidate(Invalidation.Render);
            }

            if (dirty[1])
            {
                after.Invalidate(Invalidation.Render);
            }

            using var reused = new Frame(size);
            _ = renderer.AttachCommittedFrame(reused);
            stack.Render(reused.Canvas);

            using var fresh = new Frame(size);
            stack.Render(fresh.Canvas);

            reused.PlacementCount.ShouldBe(fresh.PlacementCount);

            for (var index = 0; index < fresh.PlacementCount; index++)
            {
                reused.GetPlacement(index).ShouldBe(fresh.GetPlacement(index));
            }

            for (var row = 0; row < size.Height; row++)
            {
                Row(reused, row).ShouldBe(Row(fresh, row));
            }

            _ = await renderer.RenderAsync(reused, transport, profile, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Verifies the render-clean reuse extension point itself is invoked exactly when the
    /// copy path is taken and never on an ordinary full render, proving the general wiring added
    /// for image reuse is correct independent of which concrete control exercises it.</summary>
    [Fact]
    public async Task Render_WhenLeafIsReused_InvokesReuseHookInsteadOfFullRenderAsync()
    {
        var dirty = new ProbeControl(new Size(4, 1)) { Content = "AAAA".AsMemory() };
        var clean = new ProbeControl(new Size(4, 1)) { Content = "BBBB".AsMemory() };
        var stack = new Stack { Children = { dirty, clean } };
        var size = new Size(4, 2);
        new LayoutEngine().Layout(stack, size);
        using var renderer = new Renderer();
        var transport = new ConsoleApplicationTransport();
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
        using var first = new Frame(size);
        stack.Render(first.Canvas);
        dirty.RenderCalls.ShouldBe(1);
        dirty.ReuseCleanRenderCalls.ShouldBe(0);
        clean.RenderCalls.ShouldBe(1);
        clean.ReuseCleanRenderCalls.ShouldBe(0);
        _ = await renderer.RenderAsync(first, transport, profile, TestContext.Current.CancellationToken);

        dirty.Invalidate(Invalidation.Render);
        using var second = new Frame(size);
        _ = renderer.AttachCommittedFrame(second);

        stack.Render(second.Canvas);

        dirty.RenderCalls.ShouldBe(2);
        dirty.ReuseCleanRenderCalls.ShouldBe(0);
        clean.RenderCalls.ShouldBe(1);
        clean.ReuseCleanRenderCalls.ShouldBe(1);
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
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
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

    /// <summary>Verifies three render-clean rows sitting underneath a stationary, unchanged open
    /// popup still match a fully fresh render cell-for-cell after taking the copy path - the
    /// popup layer repaints unconditionally on every frame (<c>RenderOwnedPopupDescendants</c>
    /// runs from Root every call, never gated by any clean check), so whichever byte a contested
    /// cell ends up with is always the popup's current-frame paint, regardless of whether the row
    /// underneath copied or freshly painted its own now-overwritten contribution.</summary>
    [Fact]
    public async Task Render_WhenStationaryOpenPopupOverlapsCleanRows_MatchesFullRenderEveryFrameAsync()
    {
        var (overlay, rowA, rowB, rowC, _) = BuildOverlappedFixture(popupOpen: true);
        var size = new Size(10, 3);
        new LayoutEngine().Layout(overlay, size);
        using var renderer = new Renderer();
        var transport = new ConsoleApplicationTransport();
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
        using var warm = new Frame(size);
        overlay.Render(warm.Canvas);
        _ = await renderer.RenderAsync(warm, transport, profile, TestContext.Current.CancellationToken);
        rowA.RenderCalls.ShouldBe(1);
        rowB.RenderCalls.ShouldBe(1);
        rowC.RenderCalls.ShouldBe(1);

        // Nothing changed - the popup stays open at the same origin and every row is still clean -
        // so the copy path is available underneath the popup's frame and body.
        using var reused = new Frame(size);
        _ = renderer.AttachCommittedFrame(reused);
        overlay.Render(reused.Canvas);

        rowA.RenderCalls.ShouldBe(1);
        rowB.RenderCalls.ShouldBe(1);
        rowC.RenderCalls.ShouldBe(1);

        // No previous frame is attached, so this independent render of the exact same current
        // state always takes the complete paint path for every leaf - the ground truth the
        // optimized render above must match cell-for-cell, including every cell the popup's frame
        // and body contest against the rows underneath.
        using var reference = new Frame(size);
        overlay.Render(reference.Canvas);

        for (var row = 0; row < size.Height; row++)
        {
            Row(reused, row).ShouldBe(Row(reference, row));
        }
    }

    /// <summary>Verifies a popup with a transparent resolved background still produces a
    /// cell-for-cell match with a fully fresh render while overlapping stationary, reused clean
    /// rows - the popup's own opacity only changes what pixels the blend produces, never whether
    /// the row underneath's reused output was safe to reuse in the first place, because that row's
    /// own paint is unaffected by the popup's existence either way.</summary>
    [Fact]
    public async Task Render_WhenTransparentPopupOverlapsCleanRows_MatchesFullRenderEveryFrameAsync()
    {
        var (overlay, rowA, rowB, rowC, popup) = BuildOverlappedFixture(popupOpen: true);
        var defaultFace = popup.Face;
        popup.Face = new Face(
            defaultFace.Foreground,
            Color.Transparent,
            defaultFace.Attributes,
            defaultFace.Underline,
            defaultFace.UnderlineColor);
        var size = new Size(10, 3);
        new LayoutEngine().Layout(overlay, size);
        using var renderer = new Renderer();
        var transport = new ConsoleApplicationTransport();
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
        using var warm = new Frame(size);
        overlay.Render(warm.Canvas);
        _ = await renderer.RenderAsync(warm, transport, profile, TestContext.Current.CancellationToken);
        rowA.RenderCalls.ShouldBe(1);
        rowB.RenderCalls.ShouldBe(1);
        rowC.RenderCalls.ShouldBe(1);

        using var reused = new Frame(size);
        _ = renderer.AttachCommittedFrame(reused);
        overlay.Render(reused.Canvas);

        rowA.RenderCalls.ShouldBe(1);
        rowB.RenderCalls.ShouldBe(1);
        rowC.RenderCalls.ShouldBe(1);

        using var reference = new Frame(size);
        overlay.Render(reference.Canvas);

        for (var row = 0; row < size.Height; row++)
        {
            Row(reused, row).ShouldBe(Row(reference, row));
        }
    }

    /// <summary>Verifies closing a popup that previously overlapped three render-clean rows leaves
    /// no stale popup pixels behind - closing always routes through
    /// <c>InvalidationImpact.Measure</c> (<c>Popup.SetOpen</c>'s every <c>_isOpen</c> transition
    /// pairs with a Measure notification), so production never attaches a previous frame for this
    /// render; this test matches that same no-previous-frame condition rather than the shortcut of
    /// asserting against <c>CanReuseCleanRender</c> directly.</summary>
    [Fact]
    public async Task Render_WhenPopupClosesBetweenFrames_LeavesNoStalePopupPixelsAsync()
    {
        var (overlay, rowA, rowB, rowC, popup) = BuildOverlappedFixture(popupOpen: true);
        var size = new Size(10, 3);
        new LayoutEngine().Layout(overlay, size);
        using var renderer = new Renderer();
        var transport = new ConsoleApplicationTransport();
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
        using var warm = new Frame(size);
        overlay.Render(warm.Canvas);
        _ = await renderer.RenderAsync(warm, transport, profile, TestContext.Current.CancellationToken);
        rowA.RenderCalls.ShouldBe(1);
        rowB.RenderCalls.ShouldBe(1);
        rowC.RenderCalls.ShouldBe(1);

        popup.IsOpen = false;
        new LayoutEngine().Layout(overlay, size);

        // Matches production: closing invalidated Measure, so Application never attaches a
        // previous frame for this render - no copy path is even considered anywhere this frame.
        using var afterClose = new Frame(size);
        overlay.Render(afterClose.Canvas);

        Row(afterClose, 0).ShouldBe("AAAAAAAAAA");
        Row(afterClose, 1).ShouldBe("BBBBBBBBBB");
        Row(afterClose, 2).ShouldBe("CCCCCCCCCC");

        using var reference = new Frame(size);
        overlay.Render(reference.Canvas);

        for (var row = 0; row < size.Height; row++)
        {
            Row(afterClose, row).ShouldBe(Row(reference, row));
        }
    }

    /// <summary>Verifies opening a popup over three previously popup-free, render-clean rows
    /// produces the identical result a fully fresh render would - the mirror of the closing case,
    /// covering the other direction of the same footprint-change invariant.</summary>
    [Fact]
    public async Task Render_WhenPopupOpensBetweenFrames_MatchesFullRenderImmediatelyAsync()
    {
        var (overlay, rowA, rowB, rowC, popup) = BuildOverlappedFixture(popupOpen: false);
        var size = new Size(10, 3);
        new LayoutEngine().Layout(overlay, size);
        using var renderer = new Renderer();
        var transport = new ConsoleApplicationTransport();
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
        using var warm = new Frame(size);
        overlay.Render(warm.Canvas);
        _ = await renderer.RenderAsync(warm, transport, profile, TestContext.Current.CancellationToken);
        rowA.RenderCalls.ShouldBe(1);
        rowB.RenderCalls.ShouldBe(1);
        rowC.RenderCalls.ShouldBe(1);
        Row(warm, 0).ShouldBe("AAAAAAAAAA");

        popup.IsOpen = true;
        new LayoutEngine().Layout(overlay, size);

        // Matches production: opening invalidated Measure, so no previous frame is attached here
        // either.
        using var afterOpen = new Frame(size);
        overlay.Render(afterOpen.Canvas);

        using var reference = new Frame(size);
        overlay.Render(reference.Canvas);

        for (var row = 0; row < size.Height; row++)
        {
            Row(afterOpen, row).ShouldBe(Row(reference, row));
        }

        // The popup's frame now visibly contests the rows it overlaps - sanity-checks the fixture
        // actually exercises overlap rather than two coincidentally identical blank renders.
        Row(afterOpen, 0).ShouldNotBe("AAAAAAAAAA");
    }

    /// <summary>Verifies moving an open popup between frames - without closing it - leaves no
    /// pixels from its old footprint behind and matches a fully fresh render at its new one,
    /// covering the third footprint-change case (open/close/move) alongside the two above.</summary>
    [Fact]
    public async Task Render_WhenOpenPopupMovesBetweenFrames_MatchesFullRenderAtNewPositionAsync()
    {
        var (overlay, rowA, rowB, rowC, popup) = BuildOverlappedFixture(popupOpen: true);
        var size = new Size(10, 3);
        new LayoutEngine().Layout(overlay, size);
        using var renderer = new Renderer();
        var transport = new ConsoleApplicationTransport();
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
        using var warm = new Frame(size);
        overlay.Render(warm.Canvas);
        _ = await renderer.RenderAsync(warm, transport, profile, TestContext.Current.CancellationToken);
        rowA.RenderCalls.ShouldBe(1);
        rowB.RenderCalls.ShouldBe(1);
        rowC.RenderCalls.ShouldBe(1);

        popup.FixedOrigin = new Point(0, 0);

        // FixedOrigin itself carries no change notification (Popup.cs), so force the Arrange
        // invalidation a real reposition API (Anchor, Placement) would trigger on its own -
        // FixedOrigin's branch in ArrangeOverride ignores Anchor's bounds regardless, so this only
        // supplies the invalidation, not the new position.
        popup.Anchor = new ProbeControl();
        new LayoutEngine().Layout(overlay, size);

        // Matches production: repositioning a popup only ever happens inside an Arrange pass, so
        // no previous frame is attached here either.
        using var afterMove = new Frame(size);
        overlay.Render(afterMove.Canvas);

        using var reference = new Frame(size);
        overlay.Render(reference.Canvas);

        for (var row = 0; row < size.Height; row++)
        {
            Row(afterMove, row).ShouldBe(Row(reference, row));
        }

        // Column 6 sat under the popup's old right border (origin (3, 0), width 4) and is outside
        // its new footprint (origin (0, 0), width 4) - it must show the row's own content again,
        // not a leftover border glyph from the previous origin.
        Row(afterMove, 0)[6].ShouldBe('A');
    }

    private static (Overlay Overlay, ProbeControl RowA, ProbeControl RowB, ProbeControl RowC, Popup Popup)
        BuildOverlappedFixture(bool popupOpen)
    {
        var rowA = new ProbeControl(new Size(10, 1)) { Content = "AAAAAAAAAA".AsMemory() };
        var rowB = new ProbeControl(new Size(10, 1)) { Content = "BBBBBBBBBB".AsMemory() };
        var rowC = new ProbeControl(new Size(10, 1)) { Content = "CCCCCCCCCC".AsMemory() };
        Overlay.SetTop(rowB, Length.Cells(1));
        Overlay.SetTop(rowC, Length.Cells(2));
        var popupChild = new ProbeControl(new Size(2, 1)) { Content = "PP".AsMemory() };
        var popup = new Popup { Content = popupChild, IsOpen = popupOpen, FixedOrigin = new Point(3, 0) };
        var overlay = new Overlay { Children = { rowA, rowB, rowC, popup } };
        return (overlay, rowA, rowB, rowC, popup);
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
