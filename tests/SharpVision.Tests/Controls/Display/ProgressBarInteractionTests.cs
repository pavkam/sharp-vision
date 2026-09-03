// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Display;

/// <summary>Verifies ProgressBar runtime transitions through mounted surfaces: value and endpoint
/// clamping with rendered fill, indeterminate toggling both ways, orientation flips, glyph and face
/// changes, sub-cell resolution, one-cell bounds, and resize.</summary>
public sealed class ProgressBarInteractionTests
{
    /// <summary>Verifies value assignments after layout clamp to the range, fire ValueChanged with
    /// the clamped values, and repaint the fill exactly.</summary>
    [Fact]
    public async Task Value_WhenAssignedAfterLayout_ClampsFiresAndRepaintsAsync()
    {
        // Arrange
        var bar = new ProgressBar { Width = Length.Cells(4) };
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(4, 1),
            TestContext.Current.CancellationToken);
        var changes = new List<(double Previous, double Current)>();
        bar.ValueChanged += (_, eventArgs) => changes.Add((eventArgs.PreviousValue, eventArgs.Value));
        surface.ShouldRender("░░░░");

        // Act and assert
        await surface.UpdateAsync(() => bar.Value = 0.5, "half");
        surface.ShouldRender("██░░");
        await surface.UpdateAsync(() => bar.Value = 7, "past the maximum");
        surface.ShouldRender("████");
        bar.Value.ShouldBe(1);
        await surface.UpdateAsync(() => bar.Value = -3, "below the minimum");
        surface.ShouldRender("░░░░");
        bar.Value.ShouldBe(0);
        await surface.UpdateAsync(() => bar.Value = 0, "same value again");
        changes.ShouldBe([(0, 0.5), (0.5, 1), (1, 0)]);
    }

    /// <summary>Verifies raising Minimum above the value and lowering Maximum below it clamp the
    /// value and repaint against the new range.</summary>
    [Fact]
    public async Task Endpoints_WhenChangedAfterLayout_ClampValueAndRepaintAsync()
    {
        // Arrange
        var bar = new ProgressBar { Width = Length.Cells(4), Maximum = 10, Value = 5 };
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(4, 1),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("██░░");

        // Act raise the minimum above the value
        await surface.UpdateAsync(() => bar.Minimum = 6, "raise the minimum above the value");

        // Assert the value clamps to the new minimum and the bar is empty
        bar.Value.ShouldBe(6);
        surface.ShouldRender("░░░░");

        // Act lower the maximum to the middle of the new range
        await surface.UpdateAsync(
            () =>
            {
                bar.Minimum = 0;
                bar.Value = 8;
                bar.Maximum = 8;
            },
            "make the value the new maximum");

        // Assert
        surface.ShouldRender("████");
        _ = Should.Throw<ArgumentException>(() => bar.Maximum = 0);
        _ = Should.Throw<ArgumentException>(() => bar.Minimum = 8);
    }

    /// <summary>Verifies toggling IsIndeterminate after layout swaps the whole track for the
    /// indeterminate glyph and restores the determinate fill when cleared.</summary>
    [Fact]
    public async Task IsIndeterminate_WhenToggledAfterLayout_SwapsAndRestoresFillAsync()
    {
        // Arrange
        var bar = new ProgressBar { Width = Length.Cells(4), Value = 0.5 };
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(4, 1),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("██░░");

        // Act and assert
        await surface.UpdateAsync(() => bar.IsIndeterminate = true, "enter indeterminate");
        surface.ShouldRender("▒▒▒▒");
        await surface.UpdateAsync(() => bar.Value = 1, "change the value while indeterminate");
        surface.ShouldRender("▒▒▒▒");
        await surface.UpdateAsync(() => bar.IsIndeterminate = false, "leave indeterminate");
        surface.ShouldRender("████");
    }

    /// <summary>Verifies flipping Orientation after layout re-measures the bar and fills bottom-up.</summary>
    [Fact]
    public async Task Orientation_WhenFlippedAfterLayout_RemeasuresAndFillsBottomUpAsync()
    {
        // Arrange
        var bar = new ProgressBar { Value = 0.5 };
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(10, 4),
            TestContext.Current.CancellationToken);
        surface.Cell(new Point(0, 0)).Text.ShouldBe("█");
        surface.Cell(new Point(9, 0)).Text.ShouldBe("░");
        surface.Cell(new Point(0, 1)).Text.ShouldBe(" ");

        // Act
        await surface.UpdateAsync(
            () =>
            {
                bar.Orientation = Orientation.Vertical;
                bar.HorizontalAlignment = HorizontalAlignment.Left;
            },
            "flip to vertical");

        // Assert
        bar.Bounds.Width.ShouldBe(1);
        bar.Bounds.Height.ShouldBe(4);
        surface.ShouldRender("""
                             ░
                             ░
                             █
                             █
                             """);
    }

    /// <summary>Verifies custom glyphs assigned through a local style after layout repaint every
    /// cell and clearing the style restores the code-owned glyphs.</summary>
    [Fact]
    public async Task Style_WhenGlyphsChangeAfterLayout_RepaintsAndClearingRestoresAsync()
    {
        // Arrange
        var bar = new ProgressBar { Width = Length.Cells(4), Value = 0.5 };
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(4, 1),
            TestContext.Current.CancellationToken);

        // Act
        await surface.UpdateAsync(
            () => bar.Style = bar.ActualStyle with
            {
                Glyphs = new ProgressBarGlyphs(new Rune('#'), new Rune('-'), new Rune('?'))
            },
            "assign custom glyphs");

        // Assert
        surface.ShouldRender("##--");
        await surface.UpdateAsync(() => bar.IsIndeterminate = true, "indeterminate with custom glyph");
        surface.ShouldRender("????");
        await surface.UpdateAsync(
            () =>
            {
                bar.IsIndeterminate = false;
                bar.Style = null;
            },
            "clear the local style");
        surface.ShouldRender("██░░");
    }

    /// <summary>Verifies sub-cell resolution toggled after layout draws a fractional block for a
    /// partial cell and whole cells otherwise.</summary>
    [Fact]
    public async Task UseSubCellResolution_WhenToggledAfterLayout_DrawsFractionalBlockAsync()
    {
        // Arrange
        var bar = new ProgressBar { Width = Length.Cells(4), Value = 0.375 };
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(4, 1),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("█░░░");

        // Act
        await surface.UpdateAsync(() => bar.UseSubCellResolution = true, "enable sub-cell resolution");

        // Assert
        surface.Cell(new Point(0, 0)).Text.ShouldBe("█");
        surface.Cell(new Point(1, 0)).Text.ShouldBe("▌");
        surface.Cell(new Point(2, 0)).Text.ShouldBe("░");
        await surface.UpdateAsync(() => bar.UseSubCellResolution = false, "disable sub-cell resolution");
        surface.ShouldRender("█░░░");
    }

    /// <summary>Verifies a one-cell bar renders the track until the value reaches the maximum.</summary>
    [Theory]
    [InlineData(0d, "░")]
    [InlineData(0.99d, "░")]
    [InlineData(1d, "█")]
    public async Task Render_WhenBarIsOneCellWide_ShowsTrackUntilCompleteAsync(double value, string expected)
    {
        // Arrange
        var bar = new ProgressBar { Width = Length.Cells(1), Value = value };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(1, 1),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender(expected);
    }

    /// <summary>Verifies a stretched bar re-scales its fill when the surface resizes.</summary>
    [Fact]
    public async Task ResizeAsync_WhenSurfaceWidens_RescalesTheFillAsync()
    {
        // Arrange
        var bar = new ProgressBar { Value = 0.5, HorizontalAlignment = HorizontalAlignment.Stretch };
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(4, 1),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("██░░");

        // Act
        await surface.ResizeAsync(new Size(8, 1));

        // Assert
        surface.ShouldRender("████░░░░");
    }

    /// <summary>Verifies a local face assigned after layout recolors the fill and track cells while
    /// the style's part colors keep their distinct roles.</summary>
    [Fact]
    public async Task Style_WhenPartColorsChangeAfterLayout_RecolorsFillAndTrackAsync()
    {
        // Arrange
        var bar = new ProgressBar { Width = Length.Cells(4), Value = 0.5 };
        await using var surface = await ComponentSurface.MountAsync(
            bar,
            new Size(4, 1),
            TestContext.Current.CancellationToken);

        // Act
        await surface.UpdateAsync(
            () => bar.Style = bar.ActualStyle with
            {
                FillColor = ReferenceColors.Get(2),
                TrackColor = ReferenceColors.Get(4)
            },
            "assign part colors");

        // Assert
        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldBe(ReferenceColors.Get(2));
        surface.Cell(new Point(3, 0)).Style.Foreground.ShouldBe(ReferenceColors.Get(4));
    }
}
