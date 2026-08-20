// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Verifies ColorSwatch's construction defaults, Value round-trip and invalidation, sizing,
/// and non-RGB fallback rendering.</summary>
public sealed class ColorSwatchTests
{
    /// <summary>Verifies documented construction defaults: a six-by-one RGB-red preview.</summary>
    [Fact]
    public void Constructor_WhenCreated_UsesDocumentedDefaults()
    {
        var swatch = new ColorSwatch();

        swatch.Width.ShouldBe(Length.Cells(6));
        swatch.Height.ShouldBe(Length.Cells(1));
        swatch.Value.ShouldBe(Color.Rgb(255, 0, 0));
    }

    /// <summary>Verifies Value round-trips distinct RGB colors and invalidates rendering only.</summary>
    [Fact]
    public void Value_WhenChanged_RoundTripsAndInvalidatesRenderOnly()
    {
        var swatch = new ColorSwatch();
        swatch.Clear(Invalidation.All);

        swatch.Value = Color.Rgb(10, 20, 30);

        swatch.Value.ShouldBe(Color.Rgb(10, 20, 30));
        swatch.Pending.ShouldBe(Invalidation.Render);
    }

    /// <summary>Verifies reassigning the identical Value is a no-op, matching the shared
    /// SetProperty-backed contract every other simple property follows.</summary>
    [Fact]
    public void Value_WhenReassignedTheSameValue_DoesNotInvalidate()
    {
        var swatch = new ColorSwatch { Value = Color.Rgb(1, 2, 3) };
        swatch.Clear(Invalidation.All);

        swatch.Value = Color.Rgb(1, 2, 3);

        swatch.Pending.ShouldBe(Invalidation.None);
    }

    /// <summary>Verifies Value accepts the terminal default color, which is not RGB.</summary>
    [Fact]
    public void Value_WhenAssignedDefaultColor_RoundTrips()
    {
        var swatch = new ColorSwatch { Value = Color.Default };

        swatch.Value.ShouldBe(Color.Default);
    }

    /// <summary>Verifies measurement always reports the fixed six-by-one size regardless of a
    /// wider or unconstrained incoming width.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData(20)]
    public void Measure_WhenGivenAnyWidthConstraint_ReturnsFixedSixByOneSize(int? width)
    {
        var swatch = new ColorSwatch();

        swatch.Measure(new Constraint(width, null));

        swatch.DesiredSize.ShouldBe(new Size(6, 1));
    }

    /// <summary>Verifies an RGB value fills its bounds with that exact background and a
    /// automatically computed contrasting foreground.</summary>
    [Fact]
    public void Render_WhenValueIsRgb_FillsBoundsWithBackgroundAndContrastForeground()
    {
        var swatch = new ColorSwatch { Value = Color.Rgb(255, 255, 255), Bounds = new Rect(0, 0, 6, 1) };
        using Frame frame = new(new Size(6, 1));

        swatch.Render(frame.Canvas);

        var cell = frame.GetCell(new Point(0, 0));
        cell.Style.Background.ShouldBe(Color.Rgb(255, 255, 255));
        cell.Style.Foreground.ShouldBe(Color.Rgb(255, 255, 255).Contrast());
        frame.GetCell(new Point(5, 0)).Style.Background.ShouldBe(Color.Rgb(255, 255, 255));
    }

    /// <summary>Verifies a non-RGB Value (the terminal default color) still draws with that exact
    /// background, while the foreground falls back to black's computed contrast (white) since
    /// Contrast requires a resolved RGB input the default color cannot itself supply.</summary>
    [Fact]
    public void Render_WhenValueIsNotRgb_UsesBlackFallbackOnlyForContrastForeground()
    {
        var swatch = new ColorSwatch { Value = Color.Default, Bounds = new Rect(0, 0, 6, 1) };
        using Frame frame = new(new Size(6, 1));

        swatch.Render(frame.Canvas);

        var cell = frame.GetCell(new Point(0, 0));
        cell.Style.Background.ShouldBe(Color.Default);
        cell.Style.Foreground.ShouldBe(Color.Rgb(255, 255, 255));
    }
}
