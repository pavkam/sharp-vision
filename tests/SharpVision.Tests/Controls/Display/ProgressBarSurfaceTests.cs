// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Display;

/// <summary>Verifies ProgressBar states through a mounted terminal surface.</summary>
public sealed class ProgressBarSurfaceTests
{
    /// <summary>Verifies intrinsic border and padding reserve cells around the progress track
    /// instead of letting the track paint underneath the control's own chrome.</summary>
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
        var bar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = 50,
            Padding = new Thickness(1, 0),
            Style = ProgressBarStyle.Default with { Border = border }
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(10, 3),
            TestContext.Current.CancellationToken);

        // Assert
        bar.ContentBounds.ShouldBe(new Rect(2, 1, 6, 1));
        surface.ShouldRender("""
                             +--------+
                             | ███░░░ |
                             +--------+
                             """);
    }

    /// <summary>Verifies a theme document authoring the root-level "glyphs" field reaches a
    /// mounted ProgressBar's rendered fill, track, and indeterminate cells - the ascii family's
    /// glyph trio, not the code-owned block defaults (see themes.md#glyph-families).</summary>
    [Fact]
    public async Task Render_WhenThemeAuthorsAnAsciiGlyphFamily_DrawsItsProgressBarGlyphsAsync()
    {
        // Arrange
        var bar = new ProgressBar { Minimum = 0, Maximum = 100, Value = 40 };
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(10, 1),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("████░░░░░░");

        // Act
        await surface.UpdateAsync(
            () => surface.Application.Theme = ThemeCatalog.Parse(ThemeJson.Create(glyphs: "ascii")),
            "author an ascii glyph family");

        // Assert the fill and track glyphs switch to the ascii family's own pair.
        surface.ShouldRender("####......");

        // Act and assert the indeterminate glyph too.
        await surface.UpdateAsync(() => bar.IsIndeterminate = true, "switch ProgressBar to indeterminate");
        surface.ShouldRender("??????????");
    }

    /// <summary>Verifies partial horizontal fill, style, pointer exclusion, and clamped mutation.</summary>
    [Fact]
    public async Task UpdateAsync_WhenHorizontalValueChanges_RendersPartialThenFullBarAsync()
    {
        // Arrange
        var bar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = 40,
            Style = new ProgressBarStyle(
                ProgressBarStyle.Default.Face,
                ProgressBarStyle.Default.Border,
                ProgressBarStyle.Default.Shadow,
                ReferenceColors.Get(3),
                ProgressBarStyle.Default.TrackColor,
                ProgressBarStyle.Default.IndeterminateColor,
                ProgressBarStyle.Default.Glyphs)
        };
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(10, 1),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("████░░░░░░");
        surface.Cell(new Point(3, 0)).Style.Foreground.ShouldBe(ReferenceColors.Get(3));
        surface.Cell(new Point(4, 0)).Text.ShouldBe("░");

        // Act
        await surface.Pointer.MoveToAsync(bar);
        await surface.ResizeAsync(new Size(5, 1));
        surface.ShouldRender("██░░░");
        await surface.UpdateAsync(() => bar.Value = 125, "fill ProgressBar past its maximum");

        // Assert
        bar.Value.ShouldBe(100);
        bar.IsPointerOver.ShouldBeFalse();
        bar.IsFocused.ShouldBeFalse();
        bar.IsPressed.ShouldBeFalse();
        surface.ShouldHaveState(bar, VisualState.Normal);
        surface.ShouldRender("█████");
    }

    /// <summary>Verifies vertical determinate fill starts at the bottom.</summary>
    [Fact]
    public async Task Render_WhenVerticalValueIsPartial_FillsBottomToTopAsync()
    {
        // Arrange
        var bar = new ProgressBar { Minimum = 0, Maximum = 100, Value = 40, Orientation = Orientation.Vertical };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(1, 5),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("""
                             ░
                             ░
                             ░
                             █
                             █
                             """);
    }

    /// <summary>Verifies indeterminate mode uses a deterministic distinct presentation.</summary>
    [Fact]
    public async Task Render_WhenProgressIsIndeterminate_FillsWithIndeterminateGlyphAsync()
    {
        // Arrange
        var bar = new ProgressBar { IsIndeterminate = true };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(4, 1),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("▒▒▒▒");
    }

    /// <summary>Verifies zero arranged bounds emit no progress cells.</summary>
    [Fact]
    public async Task Render_WhenProgressBarBoundsAreZero_DrawsNothingAsync()
    {
        // Arrange
        var bar = new ProgressBar
        {
            Width = Length.Cells(0),
            Height = Length.Cells(0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(2, 1),
            TestContext.Current.CancellationToken);

        // Assert
        bar.Bounds.Width.ShouldBe(0);
        bar.Bounds.Height.ShouldBe(0);
        surface.ShouldRender(string.Empty);
    }

    /// <summary>Verifies direct and ancestor-inherited disable painting, stable geometry across a
    /// genuine resize, and re-enable recovery for a mounted ProgressBar.</summary>
    [Fact]
    public async Task IsEnabled_WhenProgressBarIsDisabled_ProvesDisabledContractAsync()
    {
        // Arrange
        var bar = new ProgressBar { Minimum = 0, Maximum = 100, Value = 40 };
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(10, 1),
            TestContext.Current.CancellationToken);

        // Act — direct disable
        await surface.UpdateAsync(() => bar.IsEnabled = false, "disable ProgressBar");

        // Assert
        surface.ShouldHaveState(bar, VisualState.Disabled);

        // Arrange — ancestor-inherited disable
        var child = new ProgressBar { Minimum = 0, Maximum = 100, Value = 40 };
        var stack = new Stack { Children = { child } };
        await using var ancestorSurface = await ComponentSurface.MountAsync(
            stack,
            new Size(10, 1),
            TestContext.Current.CancellationToken);

        // Act
        await ancestorSurface.UpdateAsync(() => stack.IsEnabled = false, "disable ancestor Stack");

        // Assert
        child.EffectiveIsEnabled.ShouldBeFalse();
        ancestorSurface.ShouldHaveState(child, VisualState.Disabled);

        // Act — geometry stability across a genuine resize
        await surface.ResizeAsync(new Size(6, 3));
        var enabledBar = new ProgressBar { Minimum = 0, Maximum = 100, Value = 40 };
        await using var enabledSurface = await ComponentSurface.MountAsync(
            enabledBar,
            new Size(6, 3),
            TestContext.Current.CancellationToken);

        // Assert
        bar.Bounds.ShouldBe(enabledBar.Bounds);
        bar.DesiredSize.ShouldBe(enabledBar.DesiredSize);

        // Act — re-enable recovery
        await surface.UpdateAsync(() => bar.IsEnabled = true, "re-enable ProgressBar");

        // Assert
        surface.ShouldHaveState(bar, VisualState.Normal);
    }
}
