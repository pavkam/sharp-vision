// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Display;

using SharpVision.Tests.Input;

/// <summary>Verifies ChaseIndicator playback through a mounted terminal surface.</summary>
public sealed class ChaseIndicatorSurfaceTests
{
    /// <summary>Verifies the minimum track alternates endpoints and style changes reset phase.</summary>
    [Fact]
    public async Task AdvanceAsync_WhenLengthIsTwo_AlternatesAndResetsStyleAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var indicator = new ChaseIndicator { Length = 2, TrailLength = 0 };
        await using var surface = await ComponentSurface.MountAsync(
            indicator,
            new Size(2, 1),
            clock,
            TestContext.Current.CancellationToken);

        // Act and assert
        surface.ShouldRender("●◯");
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(200), "reach second chase endpoint");
        surface.ShouldRender("◯●");
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(200), "return to first chase endpoint");
        surface.ShouldRender("●◯");
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(200), "move before style reset");
        await surface.UpdateAsync(
            () => indicator.Style = ChaseIndicatorStyle.Diamond,
            "reset ChaseIndicator style");
        surface.ShouldRender("◆◇");
    }

    /// <summary>Verifies the exact forward and reverse cycle without duplicate endpoints.</summary>
    [ComponentBehaviorEvidence(
        typeof(ChaseIndicator),
        ComponentBehavior.Mounted |
        ComponentBehavior.HoverExcluded |
        ComponentBehavior.FocusExcluded |
        ComponentBehavior.TabExcluded |
        ComponentBehavior.DirectionalExcluded |
        ComponentBehavior.PressReleaseExcluded)]
    [Fact]
    public async Task AdvanceAsync_WhenCircleStyleRuns_BouncesAcrossTrackAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var indicator = new ChaseIndicator { TrailLength = 0 };
        await using var surface = await ComponentSurface.MountAsync(
            indicator,
            new Size(5, 1),
            clock,
            TestContext.Current.CancellationToken);
        string[] expected =
        [
            "●◯◯◯◯",
            "◯●◯◯◯",
            "◯◯●◯◯",
            "◯◯◯●◯",
            "◯◯◯◯●",
            "◯◯◯●◯",
            "◯◯●◯◯",
            "◯●◯◯◯",
            "●◯◯◯◯"
        ];

        // Act and assert
        surface.ShouldRender(expected[0]);

        for (var index = 1; index < expected.Length; index++)
        {
            await surface.AdvanceAsync(
                TimeSpan.FromMilliseconds(200),
                $"advance ChaseIndicator to frame {index}");
            surface.ShouldRender(expected[index]);
        }

        await surface.Pointer.MoveToAsync(indicator);
        indicator.IsPointerOver.ShouldBeFalse();
        indicator.IsFocused.ShouldBeFalse();
    }

    /// <summary>Verifies wrapping reaches the final position before restarting at position zero.</summary>
    [Fact]
    public async Task AdvanceAsync_WhenMovementWraps_RestartsAfterFinalPositionAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var indicator = new ChaseIndicator { Movement = ChaseMovement.Wrap, TrailLength = 0 };
        await using var surface = await ComponentSurface.MountAsync(
            indicator,
            new Size(5, 1),
            clock,
            TestContext.Current.CancellationToken);
        string[] expected =
        [
            "●◯◯◯◯",
            "◯●◯◯◯",
            "◯◯●◯◯",
            "◯◯◯●◯",
            "◯◯◯◯●",
            "●◯◯◯◯"
        ];

        // Act and assert
        for (var index = 0; index < expected.Length; index++)
        {
            surface.ShouldRender(expected[index]);

            if (index < expected.Length - 1)
            {
                await surface.AdvanceAsync(
                    TimeSpan.FromMilliseconds(200),
                    $"advance wrapping ChaseIndicator to frame {index + 1}");
            }
        }
    }

    /// <summary>Verifies an odd track shares its center and mirrors every spread frame.</summary>
    [Fact]
    public async Task AdvanceAsync_WhenOddMovementSpreads_MirrorsAroundSharedCenterAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var indicator = new ChaseIndicator { Movement = ChaseMovement.Spread, TrailLength = 0 };
        await using var surface = await ComponentSurface.MountAsync(
            indicator,
            new Size(5, 1),
            clock,
            TestContext.Current.CancellationToken);
        string[] expected =
        [
            "◯◯●◯◯",
            "◯●◯●◯",
            "●◯◯◯●",
            "◯●◯●◯",
            "◯◯●◯◯"
        ];

        // Act and assert
        for (var index = 0; index < expected.Length; index++)
        {
            surface.ShouldRender(expected[index]);

            if (index < expected.Length - 1)
            {
                await surface.AdvanceAsync(
                    TimeSpan.FromMilliseconds(200),
                    $"advance odd spreading ChaseIndicator to frame {index + 1}");
            }
        }
    }

    /// <summary>Verifies an even track starts on two central positions and remains mirrored.</summary>
    [Fact]
    public async Task AdvanceAsync_WhenEvenMovementSpreads_StartsFromCentralPairAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var indicator = new ChaseIndicator
        {
            Movement = ChaseMovement.Spread,
            Length = 6,
            TrailLength = 0
        };
        await using var surface = await ComponentSurface.MountAsync(
            indicator,
            new Size(6, 1),
            clock,
            TestContext.Current.CancellationToken);
        string[] expected =
        [
            "◯◯●●◯◯",
            "◯●◯◯●◯",
            "●◯◯◯◯●",
            "◯●◯◯●◯",
            "◯◯●●◯◯"
        ];

        // Act and assert
        for (var index = 0; index < expected.Length; index++)
        {
            surface.ShouldRender(expected[index]);

            if (index < expected.Length - 1)
            {
                await surface.AdvanceAsync(
                    TimeSpan.FromMilliseconds(200),
                    $"advance even spreading ChaseIndicator to frame {index + 1}");
            }
        }
    }

    /// <summary>Verifies changing movement resets phase and a two-cell spread remains stationary.</summary>
    [Fact]
    public async Task Movement_WhenChangedOrSpreadIsTwoCells_UsesFirstMovementFrameAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var indicator = new ChaseIndicator { Length = 2, TrailLength = 0 };
        await using var surface = await ComponentSurface.MountAsync(
            indicator,
            new Size(2, 1),
            clock,
            TestContext.Current.CancellationToken);

        // Act and assert
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(200), "advance before changing movement");
        surface.ShouldRender("◯●");
        await surface.UpdateAsync(
            () => indicator.Movement = ChaseMovement.Spread,
            "reset ChaseIndicator to spread movement");
        surface.ShouldRender("●●");
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(200), "advance stationary two-cell spread");
        surface.ShouldRender("●●");
    }

    /// <summary>Verifies every built-in style renders its exact active and inactive glyph pair.</summary>
    [Theory]
    [InlineData("Circle", "●◯◯")]
    [InlineData("Diamond", "◆◇◇")]
    [InlineData("Square", "■□□")]
    [InlineData("Up", "▲△△")]
    [InlineData("Down", "▼▽▽")]
    [InlineData("Left", "◀◁◁")]
    [InlineData("Right", "▶▷▷")]
    public async Task Render_WhenStyleChanges_UsesExactGlyphPairAsync(
        string preset,
        string expected)
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var style = preset switch
        {
            "Circle" => ChaseIndicatorStyle.Circle,
            "Diamond" => ChaseIndicatorStyle.Diamond,
            "Square" => ChaseIndicatorStyle.Square,
            "Up" => ChaseIndicatorStyle.Up,
            "Down" => ChaseIndicatorStyle.Down,
            "Left" => ChaseIndicatorStyle.Left,
            "Right" => ChaseIndicatorStyle.Right,
            _ => throw new UnreachableException()
        };
        var indicator = new ChaseIndicator { Length = 3, Style = style, TrailLength = 0 };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            indicator,
            new Size(3, 1),
            clock,
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender(expected);
    }

    /// <summary>Verifies appearance-only local and Theme changes preserve an advanced chase position.</summary>
    [Fact]
    public async Task UpdateAsync_WhenOnlyAppearanceChanges_PreservesAdvancedPhaseAsync()
    {
        var clock = new ManualTimeProvider();
        var glyphs = ChaseIndicatorStyle.Circle;
        var firstStyle = new ChaseIndicatorStyle(
            LiteralFace(Color.Rgb(0x40, 0x80, 0xC0)),
            ChaseIndicatorStyle.Default.Border,
            ChaseIndicatorStyle.Default.Shadow,
            glyphs.Active,
            glyphs.Inactive,
            ChaseIndicatorStyle.Default.HeadColor,
            ChaseIndicatorStyle.Default.TrailColor,
            ChaseIndicatorStyle.Default.TrackColor);
        var secondStyle = new ChaseIndicatorStyle(
            LiteralFace(Color.Rgb(0xC0, 0x80, 0x40)),
            ChaseIndicatorStyle.Default.Border,
            ChaseIndicatorStyle.Default.Shadow,
            glyphs.Active,
            glyphs.Inactive,
            ChaseIndicatorStyle.Default.HeadColor,
            ChaseIndicatorStyle.Default.TrailColor,
            ChaseIndicatorStyle.Default.TrackColor);
        var indicator = new ChaseIndicator
        {
            Length = 5,
            TrailLength = 0,
            Style = firstStyle
        };
        await using var surface = await ComponentSurface.MountAsync(
            indicator,
            new Size(5, 1),
            clock,
            TestContext.Current.CancellationToken);
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(200), "advance styled ChaseIndicator");
        surface.ShouldRender("◯●◯◯◯");

        await surface.UpdateAsync(
            () => indicator.Style = secondStyle,
            "replace only local ChaseIndicator appearance");

        surface.ShouldRender("◯●◯◯◯");

        var firstTheme = CreateTheme(firstStyle);
        var secondTheme = CreateTheme(secondStyle);
        await surface.UpdateAsync(
            () =>
            {
                indicator.PropagateTheme(firstTheme);
                indicator.Style = null;
            },
            "restore Theme-owned ChaseIndicator appearance");
        surface.ShouldRender("◯●◯◯◯");

        await surface.UpdateAsync(
            () => indicator.PropagateTheme(secondTheme),
            "swap only Theme-owned ChaseIndicator appearance");

        surface.ShouldRender("◯●◯◯◯");
    }

    /// <summary>Verifies Theme palette swaps repaint default head, trail, and track colors with local glyph styling.</summary>
    [Fact]
    public async Task Theme_WhenDefaultPartColorsAreSemantic_RepaintsMountedIndicatorAsync()
    {
        var firstTheme = PartColorTheme(
            Color.Rgb(0x00, 0xFF, 0x00),
            Color.Rgb(0xFF, 0x00, 0x00));
        var secondTheme = PartColorTheme(
            Color.Rgb(0x00, 0xFF, 0xFF),
            Color.Rgb(0xFF, 0x00, 0xFF));
        var baseline = ChaseIndicatorStyle.Circle;
        var indicator = new ChaseIndicator
        {
            Length = 5,
            TrailLength = 0,
            Style = new ChaseIndicatorStyle(
                LiteralFace(Color.Rgb(0xE0, 0xE0, 0xE0)),
                ChaseIndicatorStyle.Default.Border,
                ChaseIndicatorStyle.Default.Shadow,
                baseline.Active,
                baseline.Inactive,
                ChaseIndicatorStyle.Default.HeadColor,
                ChaseIndicatorStyle.Default.TrailColor,
                ChaseIndicatorStyle.Default.TrackColor)
        };
        await using var surface = await ComponentSurface.MountAsync(
            indicator,
            new Size(5, 1),
            firstTheme,
            TestContext.Current.CancellationToken);
        var firstHead = surface.Cell(new Point(0, 0)).Style.Foreground;
        var firstTrack = surface.Cell(new Point(4, 0)).Style.Foreground;

        await surface.UpdateAsync(
            () => indicator.PropagateTheme(secondTheme),
            "swap semantic ChaseIndicator part colors");

        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldNotBe(firstHead);
        surface.Cell(new Point(4, 0)).Style.Foreground.ShouldNotBe(firstTrack);
        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldBe(
            TerminalPalette.Project(secondTheme.ResolveColor(SemanticColor.Accent), ColorDepth.Basic16));
        surface.Cell(new Point(4, 0)).Style.Foreground.ShouldBe(
            TerminalPalette.Project(secondTheme.ResolveColor(SemanticColor.Muted), ColorDepth.Basic16));
    }

    /// <summary>Verifies the default trail endpoint follows the Theme status palette independently.</summary>
    [Fact]
    public async Task Theme_WhenDefaultTrailColorIsSemantic_RepaintsRetainedTrailAsync()
    {
        var clock = new ManualTimeProvider();
        var firstTheme = PartColorTheme(
            Color.Rgb(0xFF, 0xFF, 0xFF),
            Color.Rgb(0xFF, 0x00, 0x00));
        var secondTheme = PartColorTheme(
            Color.Rgb(0xFF, 0xFF, 0xFF),
            Color.Rgb(0xFF, 0x00, 0xFF));
        var indicator = new ChaseIndicator
        {
            Length = 5,
            TrailLength = 1,
            Interval = TimeSpan.FromSeconds(1),
            FadeDuration = TimeSpan.FromMilliseconds(400),
            Style = new ChaseIndicatorStyle(
                ChaseIndicatorStyle.Default.Face,
                ChaseIndicatorStyle.Default.Border,
                ChaseIndicatorStyle.Default.Shadow,
                ChaseIndicatorStyle.Default.Active,
                ChaseIndicatorStyle.Default.Inactive,
                Color.Rgb(0xFF, 0xFF, 0xFF),
                ChaseIndicatorStyle.Default.TrailColor,
                Color.Rgb(0x00, 0x00, 0xFF))
        };
        await using var surface = await ComponentSurface.MountAsync(
            indicator,
            new Size(5, 1),
            clock,
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(
            () => indicator.PropagateTheme(firstTheme),
            "apply initial semantic ChaseIndicator trail endpoint");
        await surface.AdvanceAsync(TimeSpan.FromSeconds(1), "create retained ChaseIndicator trail");
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(400), "fade trail to semantic endpoint");
        var firstTrail = surface.Cell(new Point(0, 0)).Style.Foreground;

        await surface.UpdateAsync(
            () => indicator.PropagateTheme(secondTheme),
            "swap semantic ChaseIndicator trail endpoint");

        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldNotBe(firstTrail);
        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldBe(
            TerminalPalette.Project(secondTheme.ResolveColor(SemanticColor.Muted), ColorDepth.Basic16));
    }

    /// <summary>Verifies vertical spacing and cyclic trail history through one endpoint.</summary>
    [Fact]
    public async Task AdvanceAsync_WhenVerticalTrackIsSpaced_RendersFadeHistoryThroughBounceAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var indicator = new ChaseIndicator { Length = 3, Orientation = Orientation.Vertical, Spacing = 1 };
        await using var surface = await ComponentSurface.MountAsync(
            indicator,
            new Size(1, 5),
            clock,
            TestContext.Current.CancellationToken);

        // Act and assert
        surface.ShouldRender("●\n \n●\n \n●");
        await surface.AdvanceAsync(
            TimeSpan.FromMilliseconds(200),
            "advance vertical fading chase");
        surface.ShouldRender("●\n \n●\n \n◯");
        await surface.AdvanceAsync(
            TimeSpan.FromMilliseconds(200),
            "reach vertical chase endpoint");
        surface.ShouldRender("●\n \n●\n \n●");
    }

    /// <summary>Verifies every abandoned head fades on its own clock between movement ticks.</summary>
    [Fact]
    public async Task AdvanceAsync_WhenTrailFades_UpdatesIndependentColorsAndFreezesWhenPausedAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var indicator = new ChaseIndicator
        {
            Style = new ChaseIndicatorStyle(
                ChaseIndicatorStyle.Default.Face,
                ChaseIndicatorStyle.Default.Border,
                ChaseIndicatorStyle.Default.Shadow,
                ChaseIndicatorStyle.Default.Active,
                ChaseIndicatorStyle.Default.Inactive,
                Color.Rgb(255, 255, 255),
                Color.Rgb(0, 0, 0),
                Color.Rgb(0, 0, 0)),
            FadeDuration = TimeSpan.FromMilliseconds(400),
        };
        await using var surface = await ComponentSurface.MountAsync(
            indicator,
            new Size(5, 1),
            clock,
            TestContext.Current.CancellationToken);

        // Act and assert: N - 1 starts at the head color while N - 2 is older.
        surface.Cell(new Point(1, 0)).Style.Foreground.ShouldBe(
            TerminalPalette.Project(Color.Rgb(255, 255, 255), ColorDepth.Basic16));
        surface.Cell(new Point(2, 0)).Style.Foreground.ShouldBe(
            TerminalPalette.Project(Color.Rgb(128, 128, 128), ColorDepth.Basic16));

        await surface.AdvanceAsync(
            TimeSpan.FromMilliseconds(50),
            "refresh independent chase fades");
        surface.Cell(new Point(1, 0)).Style.Foreground.ShouldBe(
            TerminalPalette.Project(Color.Rgb(223, 223, 223), ColorDepth.Basic16));
        surface.Cell(new Point(2, 0)).Style.Foreground.ShouldBe(
            TerminalPalette.Project(Color.Rgb(96, 96, 96), ColorDepth.Basic16));

        await surface.AdvanceAsync(
            TimeSpan.FromMilliseconds(150),
            "move the chase head after intermediate fades");
        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldBe(
            TerminalPalette.Project(Color.Rgb(255, 255, 255), ColorDepth.Basic16));
        surface.Cell(new Point(1, 0)).Style.Foreground.ShouldBe(
            TerminalPalette.Project(Color.Rgb(255, 255, 255), ColorDepth.Basic16));

        await surface.UpdateAsync(() => indicator.IsPlaying = false, "pause chase fades");
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(200), "hold paused chase fades");
        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldBe(
            TerminalPalette.Project(Color.Rgb(255, 255, 255), ColorDepth.Basic16));

        await surface.UpdateAsync(() => indicator.IsPlaying = true, "resume chase fades");
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(50), "continue chase fades");
        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldBe(
            TerminalPalette.Project(Color.Rgb(223, 223, 223), ColorDepth.Basic16));

        await surface.UpdateAsync(
            () => indicator.Interval = TimeSpan.FromMilliseconds(400),
            "replace movement interval without recoloring trail");
        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldBe(
            TerminalPalette.Project(Color.Rgb(223, 223, 223), ColorDepth.Basic16));
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(100), "fade under new movement interval");
        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldBe(
            TerminalPalette.Project(Color.Rgb(159, 159, 159), ColorDepth.Basic16));
    }

    /// <summary>Verifies raising TrailLength at runtime immediately backfills the newly retained
    /// slots as fully faded history rather than leaving them empty until new movement ticks
    /// populate them — the exact seam ResizeTrail's synthesized <c>startedAt</c> values exist to
    /// prove, since production code never observably distinguishes freshly resized capacity from
    /// capacity that has simply always been there.</summary>
    [Fact]
    public async Task TrailLength_WhenIncreasedAtRuntime_BackfillsFullyFadedHistoryImmediatelyAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var baseline = ChaseIndicatorStyle.Default;
        var indicator = new ChaseIndicator
        {
            Movement = ChaseMovement.Wrap,
            Length = 5,
            TrailLength = 0,
            Style = new ChaseIndicatorStyle(
                baseline.Face,
                baseline.Border,
                baseline.Shadow,
                baseline.Active,
                baseline.Inactive,
                Color.Rgb(255, 255, 255),
                Color.Rgb(0, 0, 0),
                Color.Rgb(15, 15, 15))
        };
        await using var surface = await ComponentSurface.MountAsync(
            indicator,
            new Size(5, 1),
            clock,
            TestContext.Current.CancellationToken);

        // Act: advance the head to position 2 with no trail retained (TrailLength = 0).
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(200), "advance wrapping head to position 1");
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(200), "advance wrapping head to position 2");
        await surface.UpdateAsync(() => indicator.TrailLength = 2, "raise TrailLength at runtime");

        // Assert
        indicator.TrailLength.ShouldBe(2);
        surface.Cell(new Point(2, 0)).Style.Foreground.ShouldBe(
            TerminalPalette.Project(Color.Rgb(255, 255, 255), ColorDepth.Basic16));
        surface.Cell(new Point(1, 0)).Style.Foreground.ShouldBe(
            TerminalPalette.Project(Color.Rgb(0, 0, 0), ColorDepth.Basic16));
        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldBe(
            TerminalPalette.Project(Color.Rgb(0, 0, 0), ColorDepth.Basic16));
        surface.Cell(new Point(3, 0)).Style.Foreground.ShouldBe(
            TerminalPalette.Project(Color.Rgb(15, 15, 15), ColorDepth.Basic16));
        surface.Cell(new Point(4, 0)).Style.Foreground.ShouldBe(
            TerminalPalette.Project(Color.Rgb(15, 15, 15), ColorDepth.Basic16));
    }

    /// <summary>Verifies a wrapping endpoint remains and fades after the head restarts.</summary>
    [Fact]
    public async Task AdvanceAsync_WhenWrapMoves_FadesAbandonedPositionGraduallyAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var indicator = new ChaseIndicator
        {
            Movement = ChaseMovement.Wrap,
            TrailLength = 1,
            Style = new ChaseIndicatorStyle(
                ChaseIndicatorStyle.Default.Face,
                ChaseIndicatorStyle.Default.Border,
                ChaseIndicatorStyle.Default.Shadow,
                ChaseIndicatorStyle.Default.Active,
                ChaseIndicatorStyle.Default.Inactive,
                Color.Rgb(255, 255, 255),
                Color.Rgb(0, 0, 0),
                Color.Rgb(0, 0, 0)),
            FadeDuration = TimeSpan.FromMilliseconds(400)
        };
        await using var surface = await ComponentSurface.MountAsync(
            indicator,
            new Size(5, 1),
            clock,
            TestContext.Current.CancellationToken);

        // Act
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(200), "move wrapping ChaseIndicator");
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(50), "fade wrapping ChaseIndicator tail");

        // Assert
        surface.Cell(new Point(0, 0)).Style.Foreground.ShouldBe(
            TerminalPalette.Project(Color.Rgb(223, 223, 223), ColorDepth.Basic16));
        surface.Cell(new Point(1, 0)).Style.Foreground.ShouldBe(
            TerminalPalette.Project(Color.Rgb(255, 255, 255), ColorDepth.Basic16));
    }

    /// <summary>Verifies spread frames fade both abandoned arms with one shared timestamp.</summary>
    [Fact]
    public async Task AdvanceAsync_WhenSpreadMoves_FadesMirroredArmsGraduallyAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var indicator = new ChaseIndicator
        {
            Movement = ChaseMovement.Spread,
            TrailLength = 2,
            Style = new ChaseIndicatorStyle(
                ChaseIndicatorStyle.Default.Face,
                ChaseIndicatorStyle.Default.Border,
                ChaseIndicatorStyle.Default.Shadow,
                ChaseIndicatorStyle.Default.Active,
                ChaseIndicatorStyle.Default.Inactive,
                Color.Rgb(255, 255, 255),
                Color.Rgb(0, 0, 0),
                Color.Rgb(0, 0, 0)),
            FadeDuration = TimeSpan.FromMilliseconds(400)
        };
        await using var surface = await ComponentSurface.MountAsync(
            indicator,
            new Size(5, 1),
            clock,
            TestContext.Current.CancellationToken);

        // Act and assert
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(200), "spread away from shared center");
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(50), "fade shared center");
        surface.Cell(new Point(2, 0)).Style.Foreground.ShouldBe(
            TerminalPalette.Project(Color.Rgb(223, 223, 223), ColorDepth.Basic16));

        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(150), "spread to both endpoints");
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(50), "fade both abandoned spread arms");
        var expected = TerminalPalette.Project(Color.Rgb(223, 223, 223), ColorDepth.Basic16);
        surface.Cell(new Point(1, 0)).Style.Foreground.ShouldBe(expected);
        surface.Cell(new Point(3, 0)).Style.Foreground.ShouldBe(expected);
    }

    private static Face LiteralFace(Color foreground) => AppearanceTestValues.Face(
        foreground: foreground,
        background: Color.Transparent,
        attributes: TerminalAttributes.None);

    private static Theme CreateTheme(ChaseIndicatorStyle _)
    {
        var theme = new Theme();

        theme.Freeze();
        return theme;
    }

    private static Theme PartColorTheme(Color accent, Color muted)
    {
        var theme = new Theme();
        theme.SetColor(SemanticColor.Accent, accent);
        theme.SetColor(SemanticColor.Muted, muted);
        theme.Freeze();
        return theme;
    }

    /// <summary>Verifies direct and ancestor-inherited disable painting, stable geometry across a
    /// genuine resize, and re-enable recovery for a mounted ChaseIndicator.</summary>
    [ComponentBehaviorEvidence(typeof(ChaseIndicator), ComponentBehavior.Disabled)]
    [Fact]
    public async Task IsEnabled_WhenChaseIndicatorIsDisabled_ProvesDisabledContractAsync()
    {
        // Arrange
        var indicator = new ChaseIndicator { TrailLength = 0 };
        await using var surface = await ComponentSurface.MountAsync(
            indicator,
            new Size(5, 1),
            TestContext.Current.CancellationToken);

        // Act — direct disable
        await surface.UpdateAsync(() => indicator.IsEnabled = false, "disable ChaseIndicator");

        // Assert
        surface.ShouldHaveState(indicator, VisualState.Disabled);

        // Arrange — ancestor-inherited disable
        var child = new ChaseIndicator { TrailLength = 0 };
        var stack = new Stack { Children = { child } };
        await using var ancestorSurface = await ComponentSurface.MountAsync(
            stack,
            new Size(5, 1),
            TestContext.Current.CancellationToken);

        // Act
        await ancestorSurface.UpdateAsync(() => stack.IsEnabled = false, "disable ancestor Stack");

        // Assert
        child.EffectiveIsEnabled.ShouldBeFalse();
        ancestorSurface.ShouldHaveState(child, VisualState.Disabled);

        // Act — geometry stability across a genuine resize
        await surface.ResizeAsync(new Size(7, 2));
        var enabledIndicator = new ChaseIndicator { TrailLength = 0 };
        await using var enabledSurface = await ComponentSurface.MountAsync(
            enabledIndicator,
            new Size(7, 2),
            TestContext.Current.CancellationToken);

        // Assert
        indicator.Bounds.ShouldBe(enabledIndicator.Bounds);
        indicator.DesiredSize.ShouldBe(enabledIndicator.DesiredSize);

        // Act — re-enable recovery
        await surface.UpdateAsync(() => indicator.IsEnabled = true, "re-enable ChaseIndicator");

        // Assert
        surface.ShouldHaveState(indicator, VisualState.Normal);
    }
}
