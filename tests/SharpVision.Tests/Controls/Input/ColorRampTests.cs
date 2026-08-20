// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Verifies ColorRamp's construction defaults, measurement, hit-test transparency, and
/// exact per-column hue gradient rendering.</summary>
public sealed class ColorRampTests
{
    /// <summary>Verifies documented construction defaults: one stretching cell tall, and pointer
    /// transparent so the overlaid HueSlider in ColorPicker remains directly interactive.</summary>
    [Fact]
    public void Constructor_WhenCreated_UsesDocumentedDefaults()
    {
        var ramp = new ColorRamp();

        ramp.Height.ShouldBe(Length.Cells(1));
        ramp.HorizontalAlignment.ShouldBe(HorizontalAlignment.Stretch);
        ramp.IsHitTestVisible.ShouldBeFalse();
    }

    /// <summary>Verifies the default measured size is one cell tall and a fixed 32-cell fallback
    /// width absent a constraint.</summary>
    [Fact]
    public void Measure_WhenUnconstrained_ReturnsDefaultFallbackSize()
    {
        var ramp = new ColorRamp();

        ramp.Measure(new Constraint(null, null));

        ramp.DesiredSize.ShouldBe(new Size(32, 1));
    }

    /// <summary>Verifies every column across the ramp's width draws a distinct evenly distributed
    /// hue from 0 through 359, with the leftmost column at hue 0 and the rightmost at hue 359.</summary>
    [Fact]
    public void Render_WhenWidthIsGreaterThanOne_DrawsEvenlyDistributedHueGradient()
    {
        var ramp = new ColorRamp { Bounds = new Rect(0, 0, 10, 1) };
        using Frame frame = new(new Size(10, 1));

        ramp.Render(frame.Canvas);

        for (var x = 0; x < 10; x++)
        {
            var expectedHue = x * 359 / 9;
            frame.GetCell(new Point(x, 0)).Style.Background.ShouldBe(Color.FromHsv(expectedHue, 1, 1));
        }

        frame.GetCell(new Point(0, 0)).Style.Background.ShouldBe(Color.FromHsv(0, 1, 1));
        frame.GetCell(new Point(9, 0)).Style.Background.ShouldBe(Color.FromHsv(359, 1, 1));
    }

    /// <summary>Verifies a single-cell-wide ramp draws hue zero without dividing by zero.</summary>
    [Fact]
    public void Render_WhenWidthIsOne_DrawsHueZeroWithoutDividingByZero()
    {
        var ramp = new ColorRamp { Bounds = new Rect(0, 0, 1, 1) };
        using Frame frame = new(new Size(1, 1));

        Should.NotThrow(() => ramp.Render(frame.Canvas));

        frame.GetCell(new Point(0, 0)).Style.Background.ShouldBe(Color.FromHsv(0, 1, 1));
    }

    /// <summary>Verifies a zero-width or zero-height ramp renders without throwing or drawing.</summary>
    [Fact]
    public void Render_WhenBoundsAreEmpty_DoesNotThrowOrDraw()
    {
        var ramp = new ColorRamp { Bounds = default };
        using Frame frame = new(new Size(1, 1));

        Should.NotThrow(() => ramp.Render(frame.Canvas));
    }
}
