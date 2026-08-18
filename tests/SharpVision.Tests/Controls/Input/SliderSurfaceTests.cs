// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Proves Slider behavior through mounted terminal input and semantic output.</summary>
public sealed class SliderSurfaceTests
{
    /// <summary>Verifies a horizontal Slider at 50% renders the fill, thumb, and track glyphs at their expected positions.</summary>
    [Fact]
    public async Task Render_WhenHorizontalSliderIsAtFiftyPercent_DrawsExpectedTrackGlyphsAsync()
    {
        // Arrange
        var slider = new Slider
        {
            Maximum = 100,
            Value = 50,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            slider,
            new Size(11, 1),
            TestContext.Current.CancellationToken);

        // Assert — fill ━, thumb ◆, track ─.
        surface.ShouldRender("━━━━━◆─────");
    }

    /// <summary>Verifies changing value from 0 to Maximum extends the fill across the entire track.</summary>
    [Fact]
    public async Task Render_WhenValueChangesFromZeroToMaximum_FillExtendsToEndAsync()
    {
        // Arrange
        var slider = new Slider
        {
            Maximum = 100,
            Value = 0,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(
            slider,
            new Size(11, 1),
            TestContext.Current.CancellationToken);

        // Assert initial — thumb at start, all track.
        surface.ShouldRender("◆──────────");

        // Act
        await surface.UpdateAsync(() => slider.Value = 100, "set Slider to Maximum");

        // Assert — thumb at end, all fill.
        surface.ShouldRender("━━━━━━━━━━◆");
    }

    /// <summary>Verifies pressing the Right arrow increases value by SmallChange.</summary>
    [ComponentBehaviorEvidence(typeof(Slider), ComponentBehavior.Directional | ComponentBehavior.KeyboardActivation)]
    [Fact]
    public async Task Keyboard_WhenRightArrowIsPressed_IncreasesValueBySmallChangeAsync()
    {
        // Arrange
        var slider = new Slider
        {
            Maximum = 100,
            Value = 0,
            SmallChange = 10,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(
            slider,
            new Size(11, 1),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        surface.ShouldHaveFocus(slider);

        // Act
        await surface.Keyboard.PressAsync(Code.Right);

        // Assert
        slider.Value.ShouldBe(10);
        surface.ShouldRender("━◆─────────");
    }

    /// <summary>Verifies the FillColor, TrackColor, and ThumbColor apply to their respective glyph cells.</summary>
    [Fact]
    public async Task Render_WhenSliderIsMounted_AppliesFillTrackThumbColorsAsync()
    {
        // Arrange
        var slider = new Slider
        {
            Maximum = 100,
            Value = 50,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            slider,
            new Size(11, 1),
            TestContext.Current.CancellationToken);

        // Assert — verify the default semantic colors are applied.
        var theme = slider.Theme.ShouldNotBeNull();
        var accent = ThemeColorHelper.Accent(theme);
        var muted = ThemeColorHelper.Muted(theme);

        // Fill cells use Accent foreground.
        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldBe(accent);
        surface.Cell(new Point(4, 0)).Style.Foreground.ShouldBe(accent);

        // Thumb uses Accent foreground.
        surface.Cell(new Point(5, 0)).Style.Foreground.ShouldBe(accent);
        surface.Cell(new Point(5, 0)).Text.ShouldBe("◆");

        // Track cells use Muted foreground.
        surface.Cell(new Point(6, 0)).Style.Foreground.ShouldBe(muted);
        surface.Cell(new Point(10, 0)).Style.Foreground.ShouldBe(muted);
    }

    /// <summary>Verifies a focused Slider renders with reverse video. Slider is borderless and
    /// every bundled theme maps focusedControl/focusedText to the exact same literal color as
    /// control/controlText, so bold alone - the only attribute a resolved Focused style would
    /// otherwise carry - has no distinct glyph on a Slider's own ◆/━/─ track in most terminal
    /// fonts. <see cref="Theme.GetInteractiveControlStyleSet"/> forces Reverse onto that
    /// otherwise-invisible focused state as a safety net.</summary>
    [Fact]
    public async Task Render_WhenSliderReceivesFocus_AppliesReverseAttributeAsync()
    {
        // Arrange
        var slider = new Slider
        {
            Maximum = 100,
            Value = 50,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(
            slider,
            new Size(11, 1),
            TestContext.Current.CancellationToken);
        (surface.Cell(new Point(5, 0)).Style.Attributes & TerminalAttributes.Reverse).ShouldBe(TerminalAttributes.None);

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert
        surface.ShouldHaveFocus(slider);
        (surface.Cell(new Point(5, 0)).Style.Attributes & TerminalAttributes.Reverse).ShouldBe(TerminalAttributes.Reverse);
    }

    /// <summary>Verifies mounted hover, focus, Tab, keys, press, selection, and unavailable cleanup.</summary>
    [Fact]
    [ComponentBehaviorEvidence(
        typeof(Slider),
        ComponentBehavior.Mounted |
        ComponentBehavior.Hover |
        ComponentBehavior.Focus |
        ComponentBehavior.Tab |
        ComponentBehavior.Directional |
        ComponentBehavior.PressRelease |
        ComponentBehavior.Activation |
        ComponentBehavior.PointerActivation |
        ComponentBehavior.KeyboardActivation |
        ComponentBehavior.UnavailableCleanup |
        ComponentBehavior.Disabled)]
    public async Task Surface_WhenInputIsDispatched_ExposesCompleteSliderBehaviorAsync()
    {
        // Arrange
        var slider = new Slider
        {
            Maximum = 100,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        var changes = 0;
        slider.ValueChanged += (_, _) => changes++;
        await using var surface = await ComponentSurface.MountAsync(
            slider,
            new Size(11, 1),
            TestContext.Current.CancellationToken);

        // Act keyboard route
        await surface.Pointer.MoveToAsync(slider, new Point(5, 0));
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Right);

        // Assert keyboard state
        slider.IsPointerOver.ShouldBeTrue();
        surface.ShouldHaveFocus(slider);
        slider.Value.ShouldBe(1);

        // Act pointer route
        await surface.Pointer.PressAsync();

        // Assert held state and rendered value
        slider.IsPressed.ShouldBeTrue();
        surface.ShouldHaveCapture(slider);
        slider.Value.ShouldBe(50);
        changes.ShouldBe(2);
        surface.Cell(new Point(5, 0)).Text.ShouldBe("◆");

        // Act release and unavailable cleanup
        await surface.Pointer.ReleaseAsync();
        slider.IsPressed.ShouldBeFalse();
        surface.ShouldHaveCapture(null);
        await surface.Pointer.MoveToAsync(slider, new Point(6, 0));
        await surface.Pointer.PressAsync();
        await surface.UpdateAsync(() => slider.IsEnabled = false, "disable held Slider");

        // Assert cleanup
        surface.ShouldHaveCapture(null);
        surface.ShouldHaveFocus(null);
        slider.IsPressed.ShouldBeFalse();
        surface.ShouldHaveState(slider, VisualState.Disabled);

        // Act re-enable and resume interaction
        await surface.UpdateAsync(() => slider.IsEnabled = true, "re-enable Slider");
        surface.ShouldHaveState(slider, VisualState.Normal);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert normal interaction resumes
        surface.ShouldHaveFocus(slider);
    }

    /// <summary>Verifies a Slider inherits disabled state from an ancestor and keeps stable geometry
    /// across a genuine resize while disabled, matching an independently-mounted enabled instance
    /// arranged at the same size.</summary>
    [Fact]
    public async Task Input_WhenAncestorDisablesSliderAndResized_InheritsStateAndPreservesGeometryAsync()
    {
        // Arrange a Slider disabled only through its ancestor
        var slider = new Slider
        {
            Maximum = 100,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var overlay = new Overlay
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Children = { slider }
        };
        await using var surface = await ComponentSurface.MountAsync(
            overlay,
            new Size(11, 1),
            TestContext.Current.CancellationToken);

        // Act disable the ancestor, not the Slider itself
        await surface.UpdateAsync(() => overlay.IsEnabled = false, "disable Slider's ancestor");

        // Assert the disabled state is inherited
        slider.EffectiveIsEnabled.ShouldBeFalse();
        surface.ShouldHaveState(slider, VisualState.Disabled);

        // Act resize to a genuinely different size while disabled
        await surface.ResizeAsync(new Size(20, 3));

        // Assert geometry matches an independently-mounted enabled instance at the same size
        var reference = new Slider
        {
            Maximum = 100,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var referenceSurface = await ComponentSurface.MountAsync(
            reference,
            new Size(20, 3),
            TestContext.Current.CancellationToken);

        slider.Bounds.ShouldBe(reference.Bounds);
        slider.DesiredSize.ShouldBe(reference.DesiredSize);
    }
}
