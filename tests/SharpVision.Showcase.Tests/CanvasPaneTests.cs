// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Tests;

/// <summary>Verifies Canvas layout and semantic custom-drawing showcase recipes.</summary>
public sealed class CanvasPaneTests
{
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
            new CanvasUnicodeSample(),
            new CanvasChartSample(),
            new CanvasPointerSample(),
        ];

        try
        {
            var rendered = new StringBuilder();

            // Act
            foreach (var sample in samples)
            {
                var size = new Size(48, 10);
                new Engine().Layout(sample, size);
                using Frame frame = new(size);
                sample.Render(frame.Canvas);
                var screen = new Screen(frame);
                screen.ValidateContinuations();
                _ = rendered.Append(screen.Text);
            }

            // Assert
            rendered.ToString().ShouldContain("Light");
            rendered.ToString().ShouldContain("░▒▓");
            rendered.ToString().ShouldContain("你好");
            rendered.ToString().ShouldContain("CPU");
            rendered.ToString().ShouldContain("Move or click");
        }
        finally
        {
            foreach (var sample in samples)
            {
                sample.Dispose();
            }
        }
    }
}
