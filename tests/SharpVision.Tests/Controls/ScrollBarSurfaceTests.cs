// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies ScrollBar appearance, range input, capture, and cleanup through mounted surfaces.</summary>
public sealed class ScrollBarSurfaceTests
{
    /// <summary>Verifies full horizontal and vertical rails expose exact buttons, track, and thumb cells.</summary>
    [Fact]
    public async Task Render_WhenFullChromeUsesDeterministicGlyphs_DrawsExactOrientationGeometryAsync()
    {
        // Arrange horizontal rail
        var horizontal = new ScrollBar
        {
            Orientation = Orientation.Horizontal,
            Chrome = ScrollBarChrome.Full,
            Maximum = 80,
            Value = 40,
            ViewportSize = 20,
            DecrementGlyph = new Rune('<'),
            IncrementGlyph = new Rune('>'),
            TrackGlyph = new Rune('.'),
            ThumbGlyph = new Rune('#'),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = Length.Cells(1),
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
            Chrome = ScrollBarChrome.Full,
            Maximum = 100,
            Value = 50,
            DecrementGlyph = new Rune('^'),
            IncrementGlyph = new Rune('v'),
            TrackGlyph = new Rune('.'),
            ThumbGlyph = new Rune('#'),
            Width = Length.Cells(1),
            VerticalAlignment = VerticalAlignment.Stretch,
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
            Chrome = ScrollBarChrome.Full,
            DecrementGlyph = new Rune('<'),
            IncrementGlyph = new Rune('>'),
            TrackGlyph = new Rune('.'),
            ThumbGlyph = new Rune('#'),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
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
        ComponentBehavior.UnavailableCleanup)]
    [Fact]
    public async Task Pointer_WhenBehaviorStateChanges_CommitsStateAndCleanupAsync()
    {
        // Arrange
        var bar = new ScrollBar
        {
            Orientation = Orientation.Horizontal,
            Chrome = ScrollBarChrome.Full,
            Width = Length.Cells(6),
            Height = Length.Cells(1),
        };
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(6, 1),
            TestContext.Current.CancellationToken);

        // Act hover and focus
        await surface.Pointer.MoveToAsync(bar, new Point(1, 0));
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert hover and focus
        surface.ShouldHaveState(bar, VisualState.PointerOver | VisualState.Focused);

        // Act press
        await surface.Pointer.PressAsync();

        // Assert press
        surface.ShouldHaveState(bar, VisualState.PointerOver | VisualState.Focused | VisualState.Pressed);

        // Act disable during capture
        await surface.UpdateAsync(() => bar.IsEnabled = false, "disable pressed ScrollBar");

        // Assert unavailable cleanup
        bar.IsPressed.ShouldBeFalse();
        bar.IsFocused.ShouldBeFalse();
        surface.ShouldHaveCapture(null);
        surface.ShouldHaveFocus(null);
        surface.ShouldHaveState(bar, VisualState.Disabled);
    }

    /// <summary>Verifies arrows, pages, and endpoints decode through terminal bytes with typed causes.</summary>
    [ComponentBehaviorEvidence(
        typeof(ScrollBar),
        ComponentBehavior.Directional |
        ComponentBehavior.Activation)]
    [Fact]
    public async Task Keyboard_WhenRangeCommandsArePressed_AppliesExactChangesAndCausesAsync()
    {
        // Arrange
        var causes = new List<Cause>();
        var bar = new ScrollBar
        {
            Orientation = Orientation.Horizontal,
            Chrome = ScrollBarChrome.Full,
            Maximum = 100,
            Value = 50,
            SmallChange = 2,
            LargeChange = 20,
            DecrementGlyph = new Rune('<'),
            IncrementGlyph = new Rune('>'),
            TrackGlyph = new Rune('.'),
            ThumbGlyph = new Rune('#'),
            Width = Length.Cells(10),
            Height = Length.Cells(1),
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
        causes.ShouldBe(Enumerable.Repeat(Cause.Keyboard, 6));
        surface.ShouldHaveState(bar, VisualState.Focused);
        surface.ShouldRender("<.....#..>");
    }

    /// <summary>Verifies buttons, track, and wheel commit exact values and preserve endpoint bubbling.</summary>
    [Fact]
    public async Task Pointer_WhenButtonsTrackAndWheelAreUsed_ReportsExactRangeCausesAsync()
    {
        // Arrange
        var changes = new List<(int Value, Cause Cause)>();
        var bar = new ScrollBar
        {
            Orientation = Orientation.Horizontal,
            Chrome = ScrollBarChrome.Full,
            Maximum = 100,
            Value = 50,
            ViewportSize = 20,
            SmallChange = 2,
            LargeChange = 20,
            DecrementGlyph = new Rune('<'),
            IncrementGlyph = new Rune('>'),
            TrackGlyph = new Rune('.'),
            ThumbGlyph = new Rune('#'),
            Width = Length.Cells(12),
            Height = Length.Cells(1),
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
        bar.Value.ShouldBe(48);
        changes.ShouldBe([
            (48, Cause.Pointer),
            (50, Cause.Pointer),
            (30, Cause.Pointer),
            (50, Cause.Pointer),
            (48, Cause.Wheel),
        ]);
        surface.ShouldRender("<....##....>");

        // Act pinned wheel
        await surface.UpdateAsync(() => bar.Value = 0, "move ScrollBar to minimum");
        changes.Clear();
        await surface.Pointer.WheelAsync(bar, new Point(6, 0), wheelX: 1);

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
            Chrome = ScrollBarChrome.Full,
            Maximum = 100,
            Width = Length.Cells(12),
            Height = Length.Cells(1),
        };
        bar.ValueChanged += (_, _) => changes++;
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(12, 1),
            TestContext.Current.CancellationToken);

        // Act press and held motion
        await surface.Pointer.MoveToAsync(bar, new Point(1, 0));
        await surface.Pointer.PressAsync();
        surface.ShouldHaveState(bar, VisualState.PointerOver | VisualState.Focused | VisualState.Pressed);
        await surface.Pointer.MovePressedToAsync(bar, new Point(6, 0));

        // Assert committed drag
        bar.Value.ShouldBe(56);
        changes.ShouldBe(1);
        surface.ShouldHaveState(bar, VisualState.PointerOver | VisualState.Focused | VisualState.Pressed);

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
}
