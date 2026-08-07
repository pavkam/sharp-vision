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
        ComponentBehavior.UnavailableCleanup)]
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
        slider.PointerOver.ShouldBeTrue();
        surface.ShouldHaveFocus(slider);
        slider.Value.ShouldBe(1);

        // Act pointer route
        await surface.Pointer.PressAsync();

        // Assert held state and rendered value
        slider.Pressed.ShouldBeTrue();
        surface.ShouldHaveCapture(slider);
        slider.Value.ShouldBe(50);
        changes.ShouldBe(2);
        surface.Cell(new Point(5, 0)).Text.ShouldBe("◆");

        // Act release and unavailable cleanup
        await surface.Pointer.ReleaseAsync();
        slider.Pressed.ShouldBeFalse();
        surface.ShouldHaveCapture(null);
        await surface.Pointer.MoveToAsync(slider, new Point(6, 0));
        await surface.Pointer.PressAsync();
        await surface.UpdateAsync(() => slider.Enabled = false, "disable held Slider");

        // Assert cleanup
        surface.ShouldHaveCapture(null);
        surface.ShouldHaveFocus(null);
        slider.Pressed.ShouldBeFalse();
    }
}
