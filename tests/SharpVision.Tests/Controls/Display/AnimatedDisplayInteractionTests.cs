// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Display;

/// <summary>Verifies Spinner and ChaseIndicator playback lifecycle through mounted surfaces with a
/// deterministic clock: pause and resume cadence, interval changes, detach and reattach, disposal
/// while animating, hidden ancestors, wide ambiguous-width fallbacks, shared-tick coalescing, and
/// runtime geometry changes.</summary>
public sealed class AnimatedDisplayInteractionTests
{
    /// <summary>Verifies pausing retains the current frame through many intervals and resuming
    /// waits one complete interval before advancing.</summary>
    [Fact]
    public async Task IsPlaying_WhenPausedThenResumed_RetainsFrameAndRestartsFullIntervalAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var spinner = new Spinner();
        await using var surface = await ComponentSurface.MountAsync(
            spinner,
            new Size(1, 1),
            clock,
            TestContext.Current.CancellationToken);
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(200), "advance to the second frame");
        surface.ShouldRender("⠙");

        // Act pause mid-interval
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(150), "part of the next interval");
        await surface.UpdateAsync(() => spinner.IsPlaying = false, "pause the spinner");
        await surface.AdvanceAsync(TimeSpan.FromSeconds(3), "long paused period");

        // Assert
        surface.ShouldRender("⠙");

        // Act resume: a complete interval is required, the earlier 150ms does not count
        await surface.UpdateAsync(() => spinner.IsPlaying = true, "resume the spinner");
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(199), "almost one interval");
        surface.ShouldRender("⠙");
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(1), "complete the interval");

        // Assert
        surface.ShouldRender("⠹");
    }

    /// <summary>Verifies changing Interval on a running spinner restarts a complete interval of the
    /// new length, and an invalid interval is rejected before mutation.</summary>
    [Fact]
    public async Task Interval_WhenChangedWhileRunning_RestartsWithTheNewLengthAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var spinner = new Spinner();
        await using var surface = await ComponentSurface.MountAsync(
            spinner,
            new Size(1, 1),
            clock,
            TestContext.Current.CancellationToken);
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(100), "half of the default interval");

        // Act
        await surface.UpdateAsync(() => spinner.Interval = TimeSpan.FromMilliseconds(300), "lengthen the interval");
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(299), "almost one new interval");
        surface.ShouldRender("⠋");
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(1), "complete the new interval");

        // Assert
        surface.ShouldRender("⠙");
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(300), "one more new interval");
        surface.ShouldRender("⠹");
        _ = Should.Throw<ArgumentOutOfRangeException>(() => spinner.Interval = TimeSpan.Zero);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => spinner.Interval = TimeSpan.FromMilliseconds(-1));
        spinner.Interval.ShouldBe(TimeSpan.FromMilliseconds(300));
    }

    /// <summary>Verifies detaching an animating spinner stops its timer without losing its frame,
    /// and re-attaching resumes from that frame after one interval.</summary>
    [Fact]
    public async Task Detach_WhenSpinnerIsRemovedAndReadded_PreservesFrameAndResumesAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var spinner = new Spinner();
        var stack = new Stack();
        stack.Children.Add(spinner);
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(1, 1),
            clock,
            TestContext.Current.CancellationToken);
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(200), "advance to the second frame");
        surface.ShouldRender("⠙");

        // Act detach
        await surface.UpdateAsync(() => stack.Children.Remove(spinner).ShouldBeTrue(), "detach the spinner");
        await surface.AdvanceAsync(TimeSpan.FromSeconds(2), "detached period");

        // Assert
        surface.ShouldRender(" ");
        spinner.Dispatcher.ShouldBeNull();

        // Act reattach
        await surface.UpdateAsync(() => stack.Children.Add(spinner), "reattach the spinner");

        // Assert
        surface.ShouldRender("⠙");
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(200), "one interval after reattach");
        surface.ShouldRender("⠹");
    }

    /// <summary>Verifies disposing an animating control releases its timer: later clock advances
    /// neither fault nor paint anything.</summary>
    [Theory]
    [InlineData("spinner")]
    [InlineData("chase")]
    public async Task Dispose_WhenAnimating_ReleasesTimerAndPaintsNothingAsync(string kind)
    {
        // Arrange
        var clock = new ManualTimeProvider();
        ControlBase animated = kind == "spinner" ? new Spinner() : new ChaseIndicator { Length = 3, TrailLength = 0 };
        var stack = new Stack();
        stack.Children.Add(animated);
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(3, 1),
            clock,
            TestContext.Current.CancellationToken);
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(200), "advance one frame");
        surface.Cell(default).Text.ShouldNotBe(" ");

        // Act
        await surface.UpdateAsync(animated.Dispose, "dispose while animating");
        await surface.AdvanceAsync(TimeSpan.FromSeconds(2), "advance after disposal");

        // Assert
        animated.IsDisposed.ShouldBeTrue();
        stack.Children.Count.ShouldBe(0);
        surface.ShouldRender("   ");
    }

    /// <summary>Verifies a hidden ancestor stops both animations and showing it resumes them from
    /// their retained frames.</summary>
    [Fact]
    public async Task Visibility_WhenAncestorHidesAndShows_StopsAndResumesBothAnimationsAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var spinner = new Spinner();
        var chase = new ChaseIndicator { Length = 3, TrailLength = 0 };
        var stack = new Stack();
        stack.Children.Add(spinner);
        stack.Children.Add(chase);
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(3, 2),
            clock,
            TestContext.Current.CancellationToken);
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(200), "advance one frame");
        surface.ShouldRender("⠙  \n◯●◯");

        // Act hide
        await surface.UpdateAsync(() => stack.Visibility = Visibility.Hidden, "hide the ancestor");
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(200), "tick that stops the timers");
        await surface.AdvanceAsync(TimeSpan.FromSeconds(5), "long hidden period");
        surface.ShouldRender("   \n   ");

        // Act show
        await surface.UpdateAsync(() => stack.Visibility = Visibility.Visible, "show the ancestor");

        // Assert frames retained, then both advance by one on the next interval
        surface.ShouldRender("⠙  \n◯●◯");
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(200), "one interval after showing");
        surface.ShouldRender("⠹  \n◯◯●");
    }

    /// <summary>Verifies two animations with the same interval advance on the same tick while a
    /// different interval keeps its own cadence.</summary>
    [Fact]
    public async Task AdvanceAsync_WhenIntervalsDiffer_EachAnimationKeepsItsOwnCadenceAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var first = new Spinner();
        var second = new Spinner();
        var slow = new Spinner { Interval = TimeSpan.FromMilliseconds(300) };
        var stack = new Stack { Orientation = Orientation.Horizontal };
        stack.Children.Add(first);
        stack.Children.Add(second);
        stack.Children.Add(slow);
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(3, 1),
            clock,
            TestContext.Current.CancellationToken);
        surface.ShouldRender("⠋⠋⠋");

        // Act
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(200), "first shared tick");
        surface.ShouldRender("⠙⠙⠋");
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(100), "slow spinner's first tick");
        surface.ShouldRender("⠙⠙⠙");
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(300), "600ms total");

        // Assert
        surface.ShouldRender("⠹⠹⠹");
    }

    /// <summary>Verifies a wide ambiguous-width terminal degrades ambiguous spinner frames and chase
    /// glyphs to their single-cell fallbacks while narrow Braille frames stay intact.</summary>
    [Fact]
    public async Task Render_WhenAmbiguousWidthIsWide_UsesSingleCellFallbacksAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var braille = new Spinner();
        var ambiguous = new Spinner
        {
            Style = SpinnerStyle.Braille with { Frames = [new Rune('○'), new Rune('●')] }
        };
        var chase = new ChaseIndicator { Length = 3, TrailLength = 0 };
        var stack = new Stack();
        stack.Children.Add(braille);
        stack.Children.Add(ambiguous);
        stack.Children.Add(chase);
        var options = TerminalOptions.Minimal with
        {
            Capabilities = TerminalCapabilities.Conservative with { AmbiguousWidth = Ambiguous.Wide }
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            stack,
            new Size(3, 3),
            clock,
            options,
            TestContext.Current.CancellationToken);

        // Assert
        surface.Cell(new Point(0, 0)).Text.ShouldBe("⠋");
        surface.Cell(new Point(0, 1)).Text.ShouldBe("|");
        surface.Cell(new Point(1, 1)).Text.ShouldBe(" ");
        surface.Cell(new Point(0, 2)).Text.ShouldBe("*");
        surface.Cell(new Point(0, 2)).Width.ShouldBe(1);
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(200), "advance every animation");
        surface.Cell(new Point(0, 1)).Text.ShouldBe("/");
        surface.Cell(new Point(1, 2)).Text.ShouldBe("*");
    }

    /// <summary>Verifies pausing the chase mid-cycle freezes its phase and resuming continues from
    /// the same position after one complete interval.</summary>
    [Fact]
    public async Task IsPlaying_WhenChasePausesMidCycle_FreezesPhaseThenContinuesAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var chase = new ChaseIndicator { Length = 4, TrailLength = 0 };
        await using var surface = await ComponentSurface.MountAsync(
            chase,
            new Size(4, 1),
            clock,
            TestContext.Current.CancellationToken);
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(200), "first move");
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(200), "second move");
        surface.ShouldRender("◯◯●◯");

        // Act
        await surface.UpdateAsync(() => chase.IsPlaying = false, "pause the chase");
        await surface.AdvanceAsync(TimeSpan.FromSeconds(4), "long paused period");
        surface.ShouldRender("◯◯●◯");
        await surface.UpdateAsync(() => chase.IsPlaying = true, "resume the chase");
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(199), "almost one interval");
        surface.ShouldRender("◯◯●◯");
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(1), "complete the interval");

        // Assert
        surface.ShouldRender("◯◯◯●");
    }

    /// <summary>Verifies flipping Orientation after layout re-measures the chase into a column and
    /// keeps animating along it.</summary>
    [Fact]
    public async Task Orientation_WhenFlippedAfterLayout_AnimatesAlongTheColumnAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var chase = new ChaseIndicator { Length = 3, TrailLength = 0 };
        await using var surface = await ComponentSurface.MountAsync(
            chase,
            new Size(3, 3),
            clock,
            TestContext.Current.CancellationToken);
        surface.ShouldRender("●◯◯\n   \n   ");

        // Act
        await surface.UpdateAsync(() => chase.Orientation = Orientation.Vertical, "flip to vertical");

        // Assert
        chase.Bounds.Width.ShouldBe(1);
        chase.Bounds.Height.ShouldBe(3);
        surface.Cell(new Point(0, 0)).Text.ShouldBe("●");
        surface.Cell(new Point(0, 1)).Text.ShouldBe("◯");
        surface.Cell(new Point(0, 2)).Text.ShouldBe("◯");
        surface.Cell(new Point(1, 0)).Text.ShouldBe(" ");
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(200), "advance along the column");
        surface.Cell(new Point(0, 1)).Text.ShouldBe("●");
        surface.Cell(new Point(0, 0)).Text.ShouldBe("◯");
    }

    /// <summary>Verifies Spacing inserts blank cells between chase positions and changing it after
    /// layout re-measures the track.</summary>
    [Fact]
    public async Task Spacing_WhenChangedAfterLayout_RemeasuresTheTrackAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var chase = new ChaseIndicator { Length = 3, TrailLength = 0 };
        await using var surface = await ComponentSurface.MountAsync(
            chase,
            new Size(6, 1),
            clock,
            TestContext.Current.CancellationToken);
        surface.ShouldRender("●◯◯   ");

        // Act
        await surface.UpdateAsync(() => chase.Spacing = 1, "space the track");

        // Assert
        chase.Bounds.Width.ShouldBe(5);
        surface.ShouldRender("● ◯ ◯ ");
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(200), "advance the spaced track");
        surface.ShouldRender("◯ ● ◯ ");
        _ = Should.Throw<ArgumentOutOfRangeException>(() => chase.Spacing = -1);
    }
}
