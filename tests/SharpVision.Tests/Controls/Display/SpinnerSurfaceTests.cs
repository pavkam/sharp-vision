// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Display;

/// <summary>Verifies Spinner playback through a mounted terminal surface.</summary>
public sealed class SpinnerSurfaceTests
{
    /// <summary>Verifies intrinsic border and padding frame the animated glyph without covering
    /// it or shifting it away from the control's content origin.</summary>
    [Fact]
    public async Task Render_WhenBorderAndPaddingAreConfigured_DrawsInsideContentBoundsAsync()
    {
        // Arrange
        var border = new Border(
            BorderSide.All,
            BorderGlyphStyle.Ascii,
            SemanticColor.ControlBorder,
            Color.Transparent,
            SemanticDecoration.Border);
        var spinner = new Spinner
        {
            IsPlaying = false,
            Padding = new Thickness(1, 0),
            Style = SpinnerStyle.Default with { Border = border }
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            spinner,
            new Size(5, 3),
            TestContext.Current.CancellationToken);

        // Assert
        spinner.ContentBounds.ShouldBe(new Rect(2, 1, 1, 1));
        surface.ShouldRender("""
                             +---+
                             | ⠋ |
                             +---+
                             """);
    }

    /// <summary>Verifies every built-in sequence completes its exact documented cycle.</summary>
    [Theory]
    [InlineData("Braille", "⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏")]
    [InlineData("DenseBraille", "⣿⣷⣯⣟⡿⢿⣻⣽")]
    [InlineData("Ascii", "|/-\\")]
    public async Task AdvanceAsync_WhenStyleCycles_RendersEveryDocumentedFrameAsync(
        string preset,
        string expected)
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var style = preset switch
        {
            "Braille" => SpinnerStyle.Braille,
            "DenseBraille" => SpinnerStyle.DenseBraille,
            "Ascii" => SpinnerStyle.Ascii,
            _ => throw new UnreachableException()
        };
        var spinner = new Spinner { Style = style };
        await using var surface = await ComponentSurface.MountAsync(
            spinner,
            new Size(1, 1),
            clock,
            TestContext.Current.CancellationToken);

        // Act and assert
        for (var index = 0; index < expected.Length; index++)
        {
            surface.ShouldRender(expected[index].ToString());
            await surface.AdvanceAsync(
                TimeSpan.FromMilliseconds(200),
                $"advance {preset} Spinner from frame {index}");
        }

        surface.ShouldRender(expected[0].ToString());
    }

    /// <summary>Verifies a theme document authoring the root-level "glyphs" field reaches a
    /// mounted Spinner's rendered frame sequence - the ascii family's four-frame rotation, not
    /// the code-owned Braille default (see themes.md#glyph-families).</summary>
    [Fact]
    public async Task Render_WhenThemeAuthorsAnAsciiGlyphFamily_DrawsItsSpinnerFramesAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var spinner = new Spinner();
        await using var surface = await ComponentSurface.MountAsync(
            spinner,
            new Size(1, 1),
            clock,
            TestContext.Current.CancellationToken);
        surface.ShouldRender("⠋");

        // Act
        await surface.UpdateAsync(
            () => surface.Application.Theme = ThemeCatalog.Parse(ThemeJson.Create(glyphs: "ascii")),
            "author an ascii glyph family");

        // Assert the frame sequence switches to the ascii family's own four-glyph rotation,
        // restarting at its first frame.
        surface.ShouldRender("|");
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(200), "advance ascii Spinner");
        surface.ShouldRender("/");
    }

    /// <summary>Verifies exact default frames, cadence, styling, and excluded interaction.</summary>
    [Fact]
    public async Task AdvanceAsync_WhenBrailleStyleRuns_RendersExactFramesAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var spinner = new Spinner { Face = AppearanceTestValues.Face(foreground: ReferenceColors.Get(3)) };
        await using var surface = await ComponentSurface.MountAsync(
            spinner,
            new Size(1, 1),
            clock,
            TestContext.Current.CancellationToken);

        // Act and assert
        surface.ShouldRender("⠋");
        surface.Cell(default).Style.Foreground.ShouldBe(ReferenceColors.Get(3));
        await surface.Pointer.MoveToAsync(spinner);
        spinner.IsPointerOver.ShouldBeFalse();
        spinner.IsFocused.ShouldBeFalse();
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(199), "hold Spinner frame");
        surface.ShouldRender("⠋");
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(1), "advance Spinner frame");
        surface.ShouldRender("⠙");
    }

    /// <summary>Verifies an invisible spinner stops its timer and resumes correctly when visible again.</summary>
    [Fact]
    public async Task AdvanceAsync_WhenHiddenThenShown_StopsAndResumesTimerAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var spinner = new Spinner();
        await using var surface = await ComponentSurface.MountAsync(
            spinner,
            new Size(1, 1),
            clock,
            TestContext.Current.CancellationToken);
        surface.ShouldRender("⠋");

        // Advance one frame.
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(200), "advance Spinner to second frame");
        surface.ShouldRender("⠙");

        // Hide the spinner. One more tick fires and stops the internal timer.
        await surface.UpdateAsync(
            () => spinner.Visibility = Visibility.Hidden,
            "hide Spinner");
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(200), "tick that stops the timer");

        // Many intervals pass while invisible — the frame must not advance.
        await surface.AdvanceAsync(TimeSpan.FromSeconds(5), "long invisible period");

        // Show the spinner again. OnRenderContent restarts the timer.
        await surface.UpdateAsync(
            () => spinner.Visibility = Visibility.Visible,
            "show Spinner");
        surface.ShouldRender("⠙");

        // One more tick after becoming visible — the frame advances from where it stopped.
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(200), "advance resumed Spinner");
        surface.ShouldRender("⠹");
    }

    /// <summary>Verifies zero arranged bounds emit no spinner cell.</summary>
    [Fact]
    public async Task Render_WhenSpinnerBoundsAreZero_DrawsNothingAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var spinner = new Spinner
        {
            Width = Length.Cells(0),
            Height = Length.Cells(0)
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            spinner,
            new Size(2, 1),
            clock,
            TestContext.Current.CancellationToken);

        // Assert
        spinner.Bounds.Width.ShouldBe(0);
        spinner.Bounds.Height.ShouldBe(0);
        surface.ShouldRender(string.Empty);
    }

    /// <summary>Verifies built-in patterns reset and paused playback retains phase.</summary>
    [Fact]
    public async Task UpdateAsync_WhenStyleOrPlaybackChanges_UsesExactBuiltInFramesAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var spinner = new Spinner { Style = SpinnerStyle.DenseBraille };
        await using var surface = await ComponentSurface.MountAsync(
            spinner,
            new Size(1, 1),
            clock,
            TestContext.Current.CancellationToken);
        surface.ShouldRender("⣿");
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(200), "advance dense Spinner");
        surface.ShouldRender("⣷");

        // Act and assert reset
        await surface.UpdateAsync(
            () => spinner.Style = SpinnerStyle.Ascii,
            "select ASCII Spinner style");
        surface.ShouldRender("|");
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(200), "advance ASCII Spinner");
        surface.ShouldRender("/");

        // Act and assert pause
        await surface.UpdateAsync(() => spinner.IsPlaying = false, "pause Spinner");
        await surface.AdvanceAsync(TimeSpan.FromSeconds(1), "hold paused Spinner");
        surface.ShouldRender("/");
        await surface.UpdateAsync(() => spinner.IsPlaying = true, "resume Spinner");
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(200), "advance resumed Spinner");
        surface.ShouldRender("-");
    }

    /// <summary>Verifies appearance changes preserve phase while the active style owner retains its frame sequence.</summary>
    [Fact]
    public async Task UpdateAsync_WhenOnlyAppearanceChanges_PreservesAdvancedFrameAsync()
    {
        var clock = new ManualTimeProvider();
        var frames = SpinnerStyle.DenseBraille.Frames;
        var firstFace = LiteralFace(Color.Rgb(0x00, 0x00, 0xFF));
        var secondFace = LiteralFace(Color.Rgb(0xFF, 0x00, 0x00));
        var firstStyle = new SpinnerStyle(firstFace, ControlStyle.NoBorder, ControlStyle.NoShadow, frames);
        var secondStyle = new SpinnerStyle(secondFace, ControlStyle.NoBorder, ControlStyle.NoShadow, frames);
        var spinner = new Spinner { Style = firstStyle };
        await using var surface = await ComponentSurface.MountAsync(
            spinner,
            new Size(1, 1),
            clock,
            TestContext.Current.CancellationToken);
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(200), "advance styled Spinner");
        surface.ShouldRender("⣷");

        await surface.UpdateAsync(
            () => spinner.Style = secondStyle,
            "replace only local Spinner appearance");

        surface.ShouldRender("⣷");
        spinner.ActualFace.Foreground.ShouldBe(Color.Rgb(0xFF, 0x00, 0x00));
        surface.Cell(default).Style.Foreground.ShouldBe(
            TerminalPalette.Project(Color.Rgb(0xFF, 0x00, 0x00), ColorDepth.Basic16));

        var firstTheme = ThemeCatalog.Parse(ThemeJson.Create(foreground: "#0000ff"));
        var secondTheme = ThemeCatalog.Parse(ThemeJson.Create(foreground: "#ff0000"));
        await surface.UpdateAsync(
            () =>
            {
                spinner.PropagateTheme(firstTheme);
                spinner.Style = null;
            },
            "restore Theme-owned Spinner appearance");
        surface.ShouldRender("⠋");

        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(200), "advance Theme-owned Spinner");
        surface.ShouldRender("⠙");

        await surface.UpdateAsync(
            () => spinner.PropagateTheme(secondTheme),
            "swap only Theme-owned Spinner appearance");

        surface.ShouldRender("⠙");
        surface.Cell(default).Style.Foreground.ShouldBe(
            TerminalPalette.Project(Color.Rgb(0xFF, 0x00, 0x00), ColorDepth.Basic16));
    }

    private static Face LiteralFace(Color foreground) => AppearanceTestValues.Face(
        foreground: foreground,
        background: Color.Default,
        attributes: TerminalAttributes.None);

    /// <summary>Verifies direct and ancestor-inherited disable painting, stable geometry across a
    /// genuine resize, and re-enable recovery for a mounted Spinner.</summary>
    [Fact]
    public async Task IsEnabled_WhenSpinnerIsDisabled_ProvesDisabledContractAsync()
    {
        // Arrange
        var spinner = new Spinner();
        await using var surface = await ComponentSurface.MountAsync(
            spinner,
            new Size(1, 1),
            TestContext.Current.CancellationToken);

        // Act — direct disable
        await surface.UpdateAsync(() => spinner.IsEnabled = false, "disable Spinner");

        // Assert
        surface.ShouldHaveState(spinner, VisualState.Disabled);

        // Arrange — ancestor-inherited disable
        var child = new Spinner();
        var stack = new Stack { Children = { child } };
        await using var ancestorSurface = await ComponentSurface.MountAsync(
            stack,
            new Size(1, 1),
            TestContext.Current.CancellationToken);

        // Act
        await ancestorSurface.UpdateAsync(() => stack.IsEnabled = false, "disable ancestor Stack");

        // Assert
        child.EffectiveIsEnabled.ShouldBeFalse();
        ancestorSurface.ShouldHaveState(child, VisualState.Disabled);

        // Act — geometry stability across a genuine resize
        await surface.ResizeAsync(new Size(3, 2));
        var enabledSpinner = new Spinner();
        await using var enabledSurface = await ComponentSurface.MountAsync(
            enabledSpinner,
            new Size(3, 2),
            TestContext.Current.CancellationToken);

        // Assert
        spinner.Bounds.ShouldBe(enabledSpinner.Bounds);
        spinner.DesiredSize.ShouldBe(enabledSpinner.DesiredSize);

        // Act — re-enable recovery
        await surface.UpdateAsync(() => spinner.IsEnabled = true, "re-enable Spinner");

        // Assert
        surface.ShouldHaveState(spinner, VisualState.Normal);
    }
}
