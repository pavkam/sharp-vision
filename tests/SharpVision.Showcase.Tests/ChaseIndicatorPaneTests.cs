// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Tests;

/// <summary>Verifies the ChaseIndicator Showcase page and representative semantic cells.</summary>
public sealed class ChaseIndicatorPaneTests
{
    /// <summary>Verifies every glyph family, custom length, and paused playback are visible.</summary>
    [Fact]
    public void Render_WhenChaseIndicatorPageBuilds_ShowsAllPatternsAndTrackStates()
    {
        // Arrange
        using var page = new ChaseIndicatorPane();
        var size = new Size(100, 100);
        new Engine().Layout(page, size);
        using Frame frame = new(size);
        var indicators = ControlTree.FindAll<ChaseIndicator>(page);

        // Act
        page.Render(frame.Canvas);

        // Assert
        foreach (var pattern in Enum.GetValues<ChasePattern>())
        {
            indicators.ShouldContain(value => value.Pattern == pattern);
        }

        indicators.ShouldContain(value => value.Length == 7);
        indicators.ShouldContain(value => !value.IsPlaying);
        var circle = indicators.First(value => value.Pattern == ChasePattern.Circle);
        Glyph(frame, new Point(circle.Bounds.X, circle.Bounds.Y)).ShouldBe("●");
        Glyph(frame, new Point(circle.Bounds.X + 1, circle.Bounds.Y)).ShouldBe("◯");
        new Screen(frame).ValidateContinuations();
    }

    private static string Glyph(Frame frame, Point point)
    {
        var length = frame.GetGraphemeByteCount(point);
        var bytes = new byte[length];
        _ = frame.CopyGrapheme(point, bytes);
        return Encoding.UTF8.GetString(bytes);
    }
}
