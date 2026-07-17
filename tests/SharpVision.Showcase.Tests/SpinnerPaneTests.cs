// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Tests;

/// <summary>Verifies the Spinner Showcase page and representative semantic cells.</summary>
public sealed class SpinnerPaneTests
{
    /// <summary>Verifies every built-in pattern and paused playback are visible.</summary>
    [Fact]
    public void Render_WhenSpinnerPageBuilds_ShowsAllPatternsAndPausedState()
    {
        // Arrange
        using var page = new SpinnerPane();
        var size = new Size(100, 80);
        new Engine().Layout(page, size);
        using Frame frame = new(size);
        var spinners = ControlTree.FindAll<Spinner>(page);

        // Act
        page.Render(frame.Canvas);

        // Assert
        spinners.ShouldContain(value => value.Pattern == SpinnerPattern.Braille && value.IsPlaying);
        spinners.ShouldContain(value => value.Pattern == SpinnerPattern.DenseBraille && value.IsPlaying);
        spinners.ShouldContain(value => value.Pattern == SpinnerPattern.Ascii && value.IsPlaying);
        spinners.ShouldContain(value => !value.IsPlaying);
        Glyph(frame, PointOf(spinners.First(value => value.Pattern == SpinnerPattern.Braille)))
            .ShouldBe("⠋");
        Glyph(frame, PointOf(spinners.First(value => value.Pattern == SpinnerPattern.DenseBraille)))
            .ShouldBe("⣿");
        Glyph(frame, PointOf(spinners.First(value => value.Pattern == SpinnerPattern.Ascii)))
            .ShouldBe("|");
        new Screen(frame).ValidateContinuations();
    }

    private static string Glyph(Frame frame, Point point)
    {
        var length = frame.GetGraphemeByteCount(point);
        var bytes = new byte[length];
        _ = frame.CopyGrapheme(point, bytes);
        return Encoding.UTF8.GetString(bytes);
    }

    private static Point PointOf(Control control) => new(control.Bounds.X, control.Bounds.Y);
}
