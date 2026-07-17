// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

using SharpVision.Tests.Input;

/// <summary>Verifies ChaseIndicator playback through a mounted terminal surface.</summary>
public sealed class ChaseIndicatorSurfaceTests
{
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
    public async Task AdvanceAsync_WhenCirclePatternRuns_BouncesAcrossTrackAsync()
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var indicator = new ChaseIndicator();
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
            "●◯◯◯◯",
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

    /// <summary>Verifies every built-in pattern renders its exact active and inactive glyph pair.</summary>
    [Theory]
    [InlineData(ChasePattern.Circle, "●◯◯")]
    [InlineData(ChasePattern.Diamond, "◆◇◇")]
    [InlineData(ChasePattern.Square, "■□□")]
    [InlineData(ChasePattern.Up, "▲△△")]
    [InlineData(ChasePattern.Down, "▼▽▽")]
    [InlineData(ChasePattern.Left, "◀◁◁")]
    [InlineData(ChasePattern.Right, "▶▷▷")]
    public async Task Render_WhenPatternChanges_UsesExactGlyphPairAsync(
        ChasePattern pattern,
        string expected)
    {
        // Arrange
        var clock = new ManualTimeProvider();
        var indicator = new ChaseIndicator { Length = 3, Pattern = pattern };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            indicator,
            new Size(3, 1),
            clock,
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender(expected);
    }
}
