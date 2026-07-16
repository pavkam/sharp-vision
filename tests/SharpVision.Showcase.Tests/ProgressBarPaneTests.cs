// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Tests;

/// <summary>Verifies the ProgressBar showcase page, screen cells, and live mutation.</summary>
public sealed class ProgressBarPaneTests
{
    /// <summary>Verifies determinate, vertical, and indeterminate specimens render exact roles.</summary>
    [Fact]
    public void Render_WhenProgressBarPageBuilds_ShowsRepresentativeStates()
    {
        // Arrange
        using var page = new ProgressBarPane();
        var size = new Size(120, 140);
        new Engine().Layout(page, size);
        using Frame frame = new(size);
        var bars = ControlTree.FindAll<ProgressBar>(page);
        var partial = bars.Single(value =>
            value.Orientation == Orientation.Horizontal &&
            value.Maximum == 100 &&
            value.Value == 42);
        var vertical = bars.Single(value => value.Orientation == Orientation.Vertical);
        var indeterminate = bars.Single(value => value.IsIndeterminate);

        // Act
        page.Render(frame.Canvas);

        // Assert
        bars.ShouldContain(value => value.Value == value.Minimum);
        bars.ShouldContain(value => value.Value == value.Maximum);
        Grapheme(frame, new Point(partial.Bounds.X, partial.Bounds.Y)).ShouldBe("█");
        var firstTrack = partial.Bounds.X + (int) Math.Floor(0.42 * partial.Bounds.Width);
        Grapheme(frame, new Point(firstTrack, partial.Bounds.Y)).ShouldBe("░");
        Grapheme(frame, new Point(vertical.Bounds.X, vertical.Bounds.Bottom - 1)).ShouldBe("█");
        Grapheme(frame, new Point(indeterminate.Bounds.X, indeterminate.Bounds.Y)).ShouldBe("▒");
        new Screen(frame).ValidateContinuations();
    }

    /// <summary>Verifies the live button mutates the retained bar and status text.</summary>
    [Fact]
    public void Advance_WhenLiveButtonRuns_UpdatesProgressAndStatus()
    {
        // Arrange
        using var page = new ProgressBarPane();
        new Engine().Layout(page, new Size(120, 140));
        var button = ControlTree.FindAll<Button>(page).Single(value =>
            value.Content is ControlText { Content: "Advance progress" });
        var live = ControlTree.FindAll<ProgressBar>(page).Single(value =>
            value.Maximum == 10 && value.Value == 3);

        // Act
        button.PerformClick();

        // Assert
        live.Value.ShouldBe(4);
        ControlTree.Text(page).ShouldContain("Live progress: 4 / 10");
    }

    private static string Grapheme(Frame frame, Point point)
    {
        var length = frame.GetGraphemeByteCount(point);
        var bytes = new byte[length];
        _ = frame.CopyGrapheme(point, bytes);
        return Encoding.UTF8.GetString(bytes);
    }
}
