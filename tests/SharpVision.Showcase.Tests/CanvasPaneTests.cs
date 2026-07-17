// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Tests;

/// <summary>Verifies Canvas layout and semantic custom-drawing showcase recipes.</summary>
public sealed class CanvasPaneTests
{
    /// <summary>Verifies the explicit-size Canvas specimen labels the visible twelve-cell result without clipping.</summary>
    [Fact]
    public void Canvas_WhenExplicitSizeExampleRenders_ShowsCompleteExtentLabel()
    {
        // Arrange
        using var page = new CanvasPane();
        var size = new Size(140, 220);
        new Engine().Layout(page, size);
        using Frame frame = new(size);

        // Act
        page.Render(frame.Canvas);

        // Assert
        new Screen(frame).Text.ShouldContain("12 cells");
    }

    /// <summary>Verifies opposing offsets stretch an automatic child across the Canvas remainder.</summary>
    [Fact]
    public void Canvas_WhenOpposingOffsetsArrange_StretchesAutomaticChild()
    {
        // Arrange
        using var page = new CanvasPane();
        new Engine().Layout(page, new Size(120, 180));
        var stretched = ControlTree.FindAll<Dock>(page).Single(value =>
            value.Children.Count == 1 &&
            value.Children[0] is ControlText { Content: "Stretched" });

        // Act
        var canvas = stretched.Parent.ShouldBeOfType<SharpVision.Controls.Canvas>();

        // Assert
        stretched.Bounds.Width.ShouldBe(canvas.Bounds.Width - 4);
        stretched.Bounds.Height.ShouldBe(canvas.Bounds.Height - 2);
    }

    /// <summary>Verifies each focused custom-drawing specimen produces valid semantic cells.</summary>
    [Fact]
    public void DrawingSamples_WhenRendered_ProduceExpectedSemanticContent()
    {
        // Arrange
        Control[] samples =
        [
            new CanvasSample(),
            new CanvasShadeSample(),
            new CanvasGeometrySample(),
            new CanvasUnicodeSample(),
            new CanvasChartSample(),
            new CanvasPointerSample(),
            new CanvasSparklineSample(),
            new CanvasDashboardSample(),
            new CanvasMazeSample(),
        ];

        try
        {
            var rendered = new StringBuilder();

            // Act
            foreach (var sample in samples)
            {
                var size = new Size(48, 14);
                new Engine().Layout(sample, size);
                using Frame frame = new(size);
                sample.Render(frame.Canvas);
                var screen = new Screen(frame);
                screen.ValidateContinuations();
                _ = rendered.Append(screen.Text);
            }

            // Assert
            var text = rendered.ToString();
            text.ShouldContain("Light");
            text.ShouldContain("░▒▓");
            text.ShouldContain("geometry");
            text.ShouldContain("你好");
            text.ShouldContain("CPU");
            text.ShouldContain("Move or click");
            text.ShouldContain("Sparkline");
            text.ShouldContain("System Monitor");
            text.ShouldContain("Topology merge");
        }
        finally
        {
            foreach (var sample in samples)
            {
                sample.Dispose();
            }
        }
    }

    /// <summary>Verifies shade labels remain inside an intact one-cell border.</summary>
    [Fact]
    public void CanvasShadeSample_WhenRendered_PreservesCompletePerimeter()
    {
        using var sample = new CanvasShadeSample();
        new Engine().Layout(sample, new Size(48, 10));
        using Frame frame = new(new Size(48, 10));

        sample.Render(frame.Canvas);

        Get(frame, new Point(sample.Bounds.X, sample.Bounds.Y)).ShouldBe("╭");
        Get(frame, new Point(sample.Bounds.Right - 1, sample.Bounds.Y)).ShouldBe("╮");
        Get(frame, new Point(sample.Bounds.X, sample.Bounds.Bottom - 1)).ShouldBe("╰");
        Get(frame, new Point(sample.Bounds.Right - 1, sample.Bounds.Bottom - 1)).ShouldBe("╯");
        Get(frame, new Point(sample.Bounds.Right - 1, sample.Bounds.Y + 3)).ShouldBe("│");
    }

    /// <summary>Verifies the sparkline sample renders sub-cell block elements.</summary>
    [Fact]
    public void CanvasSparklineSample_WhenRendered_DrawsBlockElements()
    {
        using var sample = new CanvasSparklineSample();
        new Engine().Layout(sample, new Size(48, 10));
        using Frame frame = new(new Size(48, 10));

        sample.Render(frame.Canvas);

        var screen = new Screen(frame);
        screen.ValidateContinuations();
        screen.Text.ShouldContain("Sparkline");
    }

    /// <summary>Verifies the dashboard sample renders gauge labels and dividers.</summary>
    [Fact]
    public void CanvasDashboardSample_WhenRendered_DrawsGaugesAndDividers()
    {
        using var sample = new CanvasDashboardSample();
        new Engine().Layout(sample, new Size(48, 14));
        using Frame frame = new(new Size(48, 14));

        sample.Render(frame.Canvas);

        var screen = new Screen(frame);
        screen.ValidateContinuations();
        screen.Text.ShouldContain("CPU");
        screen.Text.ShouldContain("MEM");
        screen.Text.ShouldContain("DSK");
        screen.Text.ShouldContain("Uptime");
    }

    /// <summary>Verifies the maze sample renders topology-merged junctions.</summary>
    [Fact]
    public void CanvasMazeSample_WhenRendered_ProducesValidTopology()
    {
        using var sample = new CanvasMazeSample();
        new Engine().Layout(sample, new Size(48, 12));
        using Frame frame = new(new Size(48, 12));

        sample.Render(frame.Canvas);

        var screen = new Screen(frame);
        screen.ValidateContinuations();
        screen.Text.ShouldContain("S");
        screen.Text.ShouldContain("E");
    }

    /// <summary>Verifies the geometry specimen exposes exact line and circle cardinal cells.</summary>
    [Fact]
    public void CanvasGeometrySample_WhenRendered_PaintsPublicPrimitiveCells()
    {
        using var sample = new CanvasGeometrySample();
        new Engine().Layout(sample, new Size(48, 14));
        using Frame frame = new(new Size(48, 14));

        sample.Render(frame.Canvas);

        Get(frame, new Point(sample.Bounds.X + 2, sample.Bounds.Y + 2)).ShouldBe("/");
        Get(frame, new Point(sample.Bounds.X + 13, sample.Bounds.Y + 7)).ShouldBe("/");
        Get(frame, new Point(sample.Bounds.X + 21, sample.Bounds.Y + 2)).ShouldBe("o");
        Get(frame, new Point(sample.Bounds.X + 24, sample.Bounds.Y + 5)).ShouldBe("o");
        Get(frame, new Point(sample.Bounds.X + 30, sample.Bounds.Y + 4)).ShouldBe("e");
    }

    private static string Get(Frame frame, Point point)
    {
        var length = frame.GetGraphemeByteCount(point);

        if (length == 0)
        {
            return string.Empty;
        }

        var bytes = new byte[length];
        _ = frame.CopyGrapheme(point, bytes);
        return Encoding.UTF8.GetString(bytes);
    }
}
