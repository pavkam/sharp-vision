// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Scrolling;

/// <summary>Verifies ScrollBar appearance, range input, capture, and cleanup through mounted surfaces.</summary>
public sealed class ScrollBarSurfaceTests
{
    /// <summary>Verifies Theme palette swaps repaint semantic part colors owned by a local style.</summary>
    [Fact]
    public async Task Theme_WhenLocalStyleUsesSemanticPartColors_RepaintsEveryRailPartAsync()
    {
        var firstTheme = PartColorTheme(
            Color.Rgb(0xFF, 0x00, 0x00),
            Color.Rgb(0x00, 0xFF, 0x00),
            Color.Rgb(0x00, 0x00, 0xFF));
        var secondTheme = PartColorTheme(
            Color.Rgb(0xFF, 0xFF, 0x00),
            Color.Rgb(0x00, 0xFF, 0xFF),
            Color.Rgb(0xFF, 0x00, 0xFF));
        var baseline = ScrollBarStyle.FullBlock;
        var style = new ScrollBarStyle(
            LiteralFace(Color.Rgb(0xE0, 0xE0, 0xE0)),
            ControlStyle.NoBorder,
            ControlStyle.NoShadow,
            baseline.Chrome,
            baseline.Fill,
            baseline.Glyphs,
            SemanticColor.Muted,
            SemanticColor.Accent,
            SemanticColor.ControlText);
        var bar = new ScrollBar
        {
            Orientation = Orientation.Horizontal,
            Style = style,
            Maximum = 100,
            ViewportSize = 20,
            Width = Length.Cells(6),
            Height = Length.Cells(1)
        };
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(6, 1),
            firstTheme,
            TestContext.Current.CancellationToken);

        var firstButton = surface.Cell(new Point(0, 0)).Style.Foreground;
        var firstThumb = surface.Cell(new Point(1, 0)).Style.Foreground;
        var firstTrack = surface.Cell(new Point(3, 0)).Style.Foreground;

        await surface.UpdateAsync(
            () => bar.PropagateTheme(secondTheme),
            "swap semantic ScrollBar part colors");

        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldNotBe(firstButton);
        surface.Cell(new Point(1, 0)).Style.Foreground.ShouldNotBe(firstThumb);
        surface.Cell(new Point(3, 0)).Style.Foreground.ShouldNotBe(firstTrack);
        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldBe(
            TerminalPalette.Project(secondTheme.ResolveColor(SemanticColor.ControlText), ColorDepth.Basic16));
        surface.Cell(new Point(1, 0)).Style.Foreground.ShouldBe(
            TerminalPalette.Project(secondTheme.ResolveColor(SemanticColor.Accent), ColorDepth.Basic16));
        surface.Cell(new Point(3, 0)).Style.Foreground.ShouldBe(
            TerminalPalette.Project(secondTheme.ResolveColor(SemanticColor.Muted), ColorDepth.Basic16));
    }

    /// <summary>Verifies full horizontal and vertical rails expose exact buttons, track, and thumb cells.</summary>
    [Fact]
    public async Task Render_WhenFullChromeUsesDeterministicGlyphs_DrawsExactOrientationGeometryAsync()
    {
        // Arrange horizontal rail
        var horizontal = new ScrollBar
        {
            Orientation = Orientation.Horizontal,
            Style = WithBlockGlyphs(
                horizontalDecrement: new Rune('<'),
                horizontalIncrement: new Rune('>'),
                track: new Rune('.'),
                thumb: new Rune('#')),
            Maximum = 80,
            Value = 40,
            ViewportSize = 20,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = Length.Cells(1)
        };
        await using var horizontalSurface = await ComponentSurface.MountAsync(
            horizontal,
            new Size(10, 1),
            TestContext.Current.CancellationToken);

        // Assert horizontal rail
        horizontalSurface.ShouldRender("<...##...>");

        // Act horizontal resize
        await horizontalSurface.ResizeAsync(new Size(6, 1));

        // Assert horizontal resize
        horizontalSurface.ShouldRender("<..#.>");

        // Arrange vertical rail
        var vertical = new ScrollBar
        {
            Style = WithBlockGlyphs(
                verticalDecrement: new Rune('^'),
                verticalIncrement: new Rune('v'),
                track: new Rune('.'),
                thumb: new Rune('#')),
            Maximum = 100,
            Value = 50,
            Width = Length.Cells(1),
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var verticalSurface = await ComponentSurface.MountAsync(
            vertical,
            new Size(1, 6),
            TestContext.Current.CancellationToken);

        // Assert vertical rail
        verticalSurface.ShouldRender("""
                                     ^
                                     .
                                     .
                                     #
                                     .
                                     v
                                     """);
    }

    /// <summary>Verifies one-, two-, and three-cell rails degrade without stale or out-of-bounds cells.</summary>
    [Fact]
    public async Task ResizeAsync_WhenFullRailIsTiny_UsesDeterministicFallbackGeometryAsync()
    {
        // Arrange
        var bar = new ScrollBar
        {
            Orientation = Orientation.Horizontal,
            Style = WithBlockGlyphs(
                horizontalDecrement: new Rune('<'),
                horizontalIncrement: new Rune('>'),
                track: new Rune('.'),
                thumb: new Rune('#')),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(1, 1),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("#");

        // Act and assert
        await surface.ResizeAsync(new Size(2, 1));
        surface.ShouldRender("<>");
        await surface.ResizeAsync(new Size(3, 1));
        surface.ShouldRender("<#>");
    }

    /// <summary>Verifies hover, focus, pressed, and disabled state commits and cleans up.</summary>
    [ComponentBehaviorEvidence(
        typeof(ScrollBar),
        ComponentBehavior.Mounted |
        ComponentBehavior.Hover |
        ComponentBehavior.Focus |
        ComponentBehavior.Tab |
        ComponentBehavior.PressRelease |
        ComponentBehavior.UnavailableCleanup |
        ComponentBehavior.Disabled)]
    [Fact]
    public async Task Pointer_WhenBehaviorStateChanges_CommitsStateAndCleanupAsync()
    {
        // Arrange
        var bar = new ScrollBar
        {
            Orientation = Orientation.Horizontal,
            Style = ScrollBarStyle.FullBlock,
            Width = Length.Cells(6),
            Height = Length.Cells(1)
        };
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(6, 1),
            TestContext.Current.CancellationToken);

        // Act hover and focus
        await surface.Pointer.MoveToAsync(bar, new Point(1, 0));
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert hover and focus
        surface.ShouldHaveState(bar, VisualState.IsPointerOver | VisualState.Focused);

        // Act press
        await surface.Pointer.PressAsync();

        // Assert press
        surface.ShouldHaveState(bar, VisualState.IsPointerOver | VisualState.Focused | VisualState.Pressed);

        // Act disable during capture
        await surface.UpdateAsync(() => bar.IsEnabled = false, "disable pressed ScrollBar");

        // Assert unavailable cleanup
        bar.IsPressed.ShouldBeFalse();
        bar.IsFocused.ShouldBeFalse();
        surface.ShouldHaveCapture(null);
        surface.ShouldHaveFocus(null);
        surface.ShouldHaveState(bar, VisualState.Disabled);

        // Act re-enable and resume interaction
        await surface.UpdateAsync(() => bar.IsEnabled = true, "re-enable ScrollBar");
        surface.ShouldHaveState(bar, VisualState.Normal);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert normal interaction resumes
        surface.ShouldHaveFocus(bar);
    }

    /// <summary>Verifies a ScrollBar inherits disabled state from an ancestor and keeps stable
    /// geometry across a genuine resize while disabled, matching an independently-mounted enabled
    /// instance arranged at the same size.</summary>
    [Fact]
    public async Task Input_WhenAncestorDisablesScrollBarAndResized_InheritsStateAndPreservesGeometryAsync()
    {
        // Arrange a ScrollBar disabled only through its ancestor
        var bar = new ScrollBar
        {
            Orientation = Orientation.Horizontal,
            Style = ScrollBarStyle.FullBlock,
            Maximum = 100,
            ViewportSize = 20,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var overlay = new Overlay
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { bar }
        };
        await using var surface = await ComponentSurface.MountAsync(
            overlay,
            new Size(6, 1),
            TestContext.Current.CancellationToken);

        // Act disable the ancestor, not the ScrollBar itself
        await surface.UpdateAsync(() => overlay.IsEnabled = false, "disable ScrollBar's ancestor");

        // Assert the disabled state is inherited
        bar.EffectiveIsEnabled.ShouldBeFalse();
        surface.ShouldHaveState(bar, VisualState.Disabled);

        // Act resize to a genuinely different size while disabled
        await surface.ResizeAsync(new Size(14, 1));

        // Assert geometry matches an independently-mounted enabled instance at the same size
        var reference = new ScrollBar
        {
            Orientation = Orientation.Horizontal,
            Style = ScrollBarStyle.FullBlock,
            Maximum = 100,
            ViewportSize = 20,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var referenceSurface = await ComponentSurface.MountAsync(
            reference,
            new Size(14, 1),
            TestContext.Current.CancellationToken);

        bar.Bounds.ShouldBe(reference.Bounds);
        bar.DesiredSize.ShouldBe(reference.DesiredSize);
    }

    /// <summary>Verifies arrows, pages, and endpoints decode through terminal bytes with typed causes.</summary>
    [ComponentBehaviorEvidence(
        typeof(ScrollBar),
        ComponentBehavior.Directional |
        ComponentBehavior.Activation |
        ComponentBehavior.KeyboardActivation)]
    [Fact]
    public async Task Keyboard_WhenRangeCommandsArePressed_AppliesExactChangesAndCausesAsync()
    {
        // Arrange
        var causes = new List<ScrollCause>();
        var bar = new ScrollBar
        {
            Orientation = Orientation.Horizontal,
            Style = WithBlockGlyphs(
                horizontalDecrement: new Rune('<'),
                horizontalIncrement: new Rune('>'),
                track: new Rune('.'),
                thumb: new Rune('#')),
            Maximum = 100,
            Value = 50,
            SmallChange = 2,
            LargeChange = 20,
            Width = Length.Cells(10),
            Height = Length.Cells(1)
        };
        bar.ValueChanged += (_, eventArgs) => causes.Add(eventArgs.Cause);
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(10, 1),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.Right);
        await surface.Keyboard.PressAsync(Code.PageDown);
        await surface.Keyboard.PressAsync(Code.Home);
        await surface.Keyboard.PressAsync(Code.End);
        await surface.Keyboard.PressAsync(Code.Left);
        await surface.Keyboard.PressAsync(Code.PageUp);

        // Assert
        bar.Value.ShouldBe(78);
        causes.ShouldBe(Enumerable.Repeat(ScrollCause.Keyboard, 6));
        surface.ShouldHaveState(bar, VisualState.Focused);
        surface.ShouldRender("<.....#..>");
    }

    /// <summary>Verifies buttons, track, and wheel commit exact values and preserve endpoint bubbling.</summary>
    [ComponentBehaviorEvidence(typeof(ScrollBar), ComponentBehavior.PointerActivation)]
    [Fact]
    public async Task Pointer_WhenButtonsTrackAndWheelAreUsed_ReportsExactRangeCausesAsync()
    {
        // Arrange
        var changes = new List<(int Value, ScrollCause Cause)>();
        var bar = new ScrollBar
        {
            Orientation = Orientation.Horizontal,
            Style = WithBlockGlyphs(
                horizontalDecrement: new Rune('<'),
                horizontalIncrement: new Rune('>'),
                track: new Rune('.'),
                thumb: new Rune('#')),
            Maximum = 100,
            Value = 50,
            ViewportSize = 20,
            SmallChange = 2,
            LargeChange = 20,
            Width = Length.Cells(12),
            Height = Length.Cells(1)
        };
        bar.ValueChanged += (_, eventArgs) => changes.Add((eventArgs.Value, eventArgs.Cause));
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(12, 1),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.ClickAsync(bar, new Point(0, 0));
        await surface.Pointer.ClickAsync(bar, new Point(11, 0));
        await surface.Pointer.ClickAsync(bar, new Point(1, 0));
        await surface.Pointer.ClickAsync(bar, new Point(10, 0));
        await surface.Pointer.WheelAsync(bar, new Point(6, 0), wheelX: 1);

        // Assert changed paths
        bar.Value.ShouldBe(52);
        changes.ShouldBe([
            (48, ScrollCause.Pointer),
            (50, ScrollCause.Pointer),
            (30, ScrollCause.Pointer),
            (50, ScrollCause.Pointer),
            (52, ScrollCause.Wheel)
        ]);
        surface.ShouldRender("<....##....>");

        // Act pinned wheel
        await surface.UpdateAsync(() => bar.Value = 0, "move ScrollBar to minimum");
        changes.Clear();
        await surface.Pointer.WheelAsync(bar, new Point(6, 0), wheelX: -1);

        // Assert pinned wheel
        bar.Value.ShouldBe(0);
        changes.ShouldBeEmpty();
    }

    /// <summary>Verifies captured drag exposes pressed state and disable cancels without a spurious commit.</summary>
    [Fact]
    public async Task Pointer_WhenThumbDragIsDisabled_CancelsCaptureAndPreservesLastCommitAsync()
    {
        // Arrange
        var changes = 0;
        var bar = new ScrollBar
        {
            Orientation = Orientation.Horizontal,
            Style = ScrollBarStyle.FullBlock,
            Maximum = 100,
            Width = Length.Cells(12),
            Height = Length.Cells(1)
        };
        bar.ValueChanged += (_, _) => changes++;
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(12, 1),
            TestContext.Current.CancellationToken);

        // Act press and held motion
        await surface.Pointer.MoveToAsync(bar, new Point(1, 0));
        await surface.Pointer.PressAsync();
        surface.ShouldHaveState(bar, VisualState.IsPointerOver | VisualState.Focused | VisualState.Pressed);
        await surface.Pointer.MovePressedToAsync(bar, new Point(6, 0));

        // Assert committed drag
        bar.Value.ShouldBe(56);
        changes.ShouldBe(1);
        surface.ShouldHaveState(bar, VisualState.IsPointerOver | VisualState.Focused | VisualState.Pressed);

        // Act cancellation and physical release
        await surface.UpdateAsync(() => bar.IsEnabled = false, "disable dragging ScrollBar");
        await surface.Pointer.ReleaseAsync();

        // Assert cancellation
        bar.Value.ShouldBe(56);
        changes.ShouldBe(1);
        bar.IsPressed.ShouldBeFalse();
        bar.IsFocused.ShouldBeFalse();
        surface.ShouldHaveState(bar, VisualState.Disabled);
    }

    /// <summary>
    /// Verifies a container resize that shrinks the track while a thumb drag is in flight moves
    /// the value proportionally to the small physical pointer movement that followed the resize,
    /// instead of snapping to an endpoint (Minimum or Maximum) as if the pre-resize absolute
    /// thumb position had been reinterpreted unchanged against the new, smaller track. The drag
    /// anchor is the value the bar held when the drag began - which has no dependency on track
    /// geometry - so a resize mid-drag must not distort what a given amount of further pointer
    /// movement means.
    /// </summary>
    [Fact]
    public async Task Pointer_WhenTrackShrinksDuringDrag_MovesValueProportionallyAsync()
    {
        // Arrange
        var bar = new ScrollBar
        {
            Orientation = Orientation.Horizontal,
            Style = ScrollBarStyle.FullBlock,
            Maximum = 100,
            Value = 50,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = Length.Cells(1)
        };
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(10, 1),
            TestContext.Current.CancellationToken);

        // Act: grab the thumb (positioned at its 50%-value location in the 10-cell bar).
        await surface.Pointer.MoveToAsync(bar, new Point(5, 0));
        await surface.Pointer.PressAsync();
        bar.Value.ShouldBe(50);

        // Act: shrink the track mid-drag, then move the pointer by only one further cell.
        await surface.ResizeAsync(new Size(8, 1));
        await surface.Pointer.MovePressedToAsync(bar, new Point(6, 0));

        // Assert: a one-cell physical move produces a moderate, proportional change - not a jump
        // all the way to Maximum, which is what reusing the pre-resize absolute thumb position
        // against the new, shorter track would have produced.
        bar.Value.ShouldBe(80);
    }

    private static ScrollBarStyle WithBlockGlyphs(
        Rune? verticalDecrement = null,
        Rune? verticalIncrement = null,
        Rune? horizontalDecrement = null,
        Rune? horizontalIncrement = null,
        Rune? track = null,
        Rune? thumb = null)
    {
        var baseline = ScrollBarStyle.FullBlock;
        var glyphs = baseline.Glyphs;
        var replacement = new ScrollBarGlyphs(
            verticalDecrement ?? glyphs.VerticalDecrement,
            verticalIncrement ?? glyphs.VerticalIncrement,
            horizontalDecrement ?? glyphs.HorizontalDecrement,
            horizontalIncrement ?? glyphs.HorizontalIncrement,
            track ?? glyphs.BlockTrack,
            thumb ?? glyphs.BlockThumb,
            glyphs.HorizontalLineTrack,
            glyphs.HorizontalLineThumb,
            glyphs.VerticalLineTrack,
            glyphs.VerticalLineThumb);

        return baseline with { Glyphs = replacement };
    }

    private static Face LiteralFace(Color foreground) => AppearanceTestValues.Face(
        foreground: foreground,
        background: Color.Transparent,
        attributes: TerminalAttributes.None);

    private static Theme PartColorTheme(Color muted, Color accent, Color controlText)
    {
        var theme = new Theme();
        theme.SetColor(SemanticColor.Muted, muted);
        theme.SetColor(SemanticColor.Accent, accent);
        theme.SetColor(SemanticColor.ControlText, controlText);
        theme.Freeze();
        return theme;
    }
}
