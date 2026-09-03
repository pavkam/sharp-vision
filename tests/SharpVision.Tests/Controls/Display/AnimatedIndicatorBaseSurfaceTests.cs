// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Display;

/// <summary>Verifies the playback and passive-input contract shared by animated indicators.</summary>
public sealed class AnimatedIndicatorBaseSurfaceTests
{
    /// <summary>Verifies the shared default synchronizes derived interval state before observers
    /// receive the committed public value.</summary>
    [Fact]
    public void Interval_WhenChanged_SynchronizesDerivedStateBeforePublication()
    {
        // Arrange
        var indicator = new AnimatedIndicatorProbe();
        var synchronizedDuringPublication = TimeSpan.Zero;
        indicator.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(AnimatedIndicatorBase.Interval))
            {
                synchronizedDuringPublication = indicator.SynchronizedInterval;
            }
        };

        // Act
        indicator.Interval = TimeSpan.FromMilliseconds(50);

        // Assert
        synchronizedDuringPublication.ShouldBe(TimeSpan.FromMilliseconds(50));
    }

    /// <summary>Verifies a derived frame cannot paint into padding even when it deliberately draws
    /// outside the content rectangle supplied by the base.</summary>
    [Fact]
    public async Task Render_WhenDerivedFrameDrawsOutsideContentBounds_ClipsPaddingCellsAsync()
    {
        // Arrange
        var indicator = new AnimatedIndicatorProbe
        {
            DrawOutsideContentBounds = true,
            IsPlaying = false,
            Padding = new Thickness(1, 0)
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            indicator,
            new Size(3, 1),
            TestContext.Current.CancellationToken);

        // Assert
        indicator.ContentBounds.ShouldBe(new Rect(1, 0, 1, 1));
        surface.ShouldRender(" 0 ");
    }

    /// <summary>Verifies cadence, pause, resume, interval changes, and pointer exclusion are owned
    /// once by the shared animated-indicator base.</summary>
    [Fact]
    public async Task Playback_WhenMounted_UsesSharedCadenceAndPassiveInputContractAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var indicator = new AnimatedIndicatorProbe { Padding = new Thickness(1, 0) };
        await using var surface = await ComponentSurface.MountAsync(
            indicator,
            new Size(3, 1),
            clock,
            TestContext.Current.CancellationToken);

        // Act and assert - the base supplies content-box rendering and an exact default interval.
        indicator.ContentBounds.ShouldBe(new Rect(1, 0, 1, 1));
        surface.ShouldRender(" 0 ");
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(199), "hold shared indicator frame");
        surface.ShouldRender(" 0 ");
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(1), "advance shared indicator frame");
        surface.ShouldRender(" 1 ");

        // Act and assert - effective invisibility suspends callbacks and visibility restarts one interval.
        await surface.UpdateAsync(() => indicator.Visibility = Visibility.Hidden, "hide shared indicator");
        await surface.AdvanceAsync(TimeSpan.FromSeconds(1), "hold hidden shared indicator frame");
        await surface.UpdateAsync(() => indicator.Visibility = Visibility.Visible, "show shared indicator");
        surface.ShouldRender(" 1 ");
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(199), "hold resumed visible indicator frame");
        surface.ShouldRender(" 1 ");
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(1), "advance visible indicator frame");
        surface.ShouldRender(" 2 ");

        // Act and assert - pause retains the frame, then a changed interval applies on resume.
        await surface.UpdateAsync(() => indicator.IsPlaying = false, "pause shared indicator playback");
        await surface.AdvanceAsync(TimeSpan.FromSeconds(1), "hold paused shared indicator frame");
        surface.ShouldRender(" 2 ");
        await surface.UpdateAsync(
            () => indicator.Interval = TimeSpan.FromMilliseconds(50),
            "change shared indicator interval");
        await surface.UpdateAsync(() => indicator.IsPlaying = true, "resume shared indicator playback");
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(50), "advance resumed shared indicator frame");
        surface.ShouldRender(" 3 ");

        // Act and assert - animated indicators are passive by default.
        await surface.Pointer.MoveToAsync(indicator);
        indicator.IsPointerOver.ShouldBeFalse();
        indicator.IsFocused.ShouldBeFalse();
    }
}
