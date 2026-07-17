// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

using SharpVision.Tests.Input;

/// <summary>Verifies Spinner playback through a mounted terminal surface.</summary>
public sealed class SpinnerSurfaceTests
{
    /// <summary>Verifies exact default frames, cadence, styling, and excluded interaction.</summary>
    [ComponentBehaviorEvidence(
        typeof(Spinner),
        ComponentBehavior.Mounted |
        ComponentBehavior.HoverExcluded |
        ComponentBehavior.FocusExcluded |
        ComponentBehavior.TabExcluded |
        ComponentBehavior.DirectionalExcluded |
        ComponentBehavior.PressReleaseExcluded)]
    [Fact]
    public async Task AdvanceAsync_WhenBraillePatternRuns_RendersExactFramesAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var spinner = new Spinner { Foreground = Color.Indexed(3) };
        await using var surface = await ComponentSurface.MountAsync(
            spinner,
            new Size(1, 1),
            clock,
            TestContext.Current.CancellationToken);

        // Act and assert
        surface.ShouldRender("⠋");
        surface.Cell(default).Style.Foreground.ShouldBe(Color.Indexed(3));
        await surface.Pointer.MoveToAsync(spinner);
        spinner.IsPointerOver.ShouldBeFalse();
        spinner.IsFocused.ShouldBeFalse();
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(199), "hold Spinner frame");
        surface.ShouldRender("⠋");
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(1), "advance Spinner frame");
        surface.ShouldRender("⠙");
    }

    /// <summary>Verifies built-in patterns reset and paused playback retains phase.</summary>
    [Fact]
    public async Task UpdateAsync_WhenPatternOrPlaybackChanges_UsesExactBuiltInFramesAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var spinner = new Spinner { Pattern = SpinnerPattern.DenseBraille };
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
            () => spinner.Pattern = SpinnerPattern.Ascii,
            "select ASCII Spinner pattern");
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
}
