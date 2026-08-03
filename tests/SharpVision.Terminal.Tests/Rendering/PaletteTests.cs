// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Rendering;

using SharpVision.Terminal.Capabilities;

/// <summary>Verifies deterministic semantic-color projection to terminal tiers.</summary>
public sealed class PaletteTests
{
    /// <summary>Provides exact reference-palette and degradation cases.</summary>
    public static TheoryData<Color, ColorDepth, Color> ProjectionCases => new()
    {
        { Color.Default, ColorDepth.Monochrome, Color.Default },
        { Color.Rgb(1, 2, 3), ColorDepth.Monochrome, Color.Default },
        { Color.Rgb(1, 2, 3), ColorDepth.TrueColor, Color.Rgb(1, 2, 3) },
        { Color.Rgb(95, 135, 175), ColorDepth.Indexed256, ReferenceColors.Get(67) },
        { Color.Rgb(128, 128, 128), ColorDepth.Indexed256, ReferenceColors.Get(244) },
        { Color.Rgb(0, 0, 0), ColorDepth.Indexed256, ReferenceColors.Get(0) },
        { Color.Rgb(255, 0, 0), ColorDepth.Basic16, ReferenceColors.Get(9) },
        { ReferenceColors.Get(231), ColorDepth.Basic16, ReferenceColors.Get(15) },
        { ReferenceColors.Get(232), ColorDepth.Basic16, ReferenceColors.Get(0) },
        { ReferenceColors.Get(255), ColorDepth.Basic16, ReferenceColors.Get(7) }
    };

    /// <summary>Verifies exact palette points and tier degradation.</summary>
    /// <param name="source">The semantic input color.</param>
    /// <param name="depth">The active terminal color tier.</param>
    /// <param name="expected">The exact projected representation.</param>
    [Theory]
    [MemberData(nameof(ProjectionCases))]
    public void Project_WhenDepthIsSelected_ReturnsExpectedRepresentation(
        Color source,
        ColorDepth depth,
        Color expected) => TerminalPalette.Project(source, depth).ShouldBe(expected);

    /// <summary>Verifies every reference RGB remains inside each target palette.</summary>
    [Fact]
    public void Project_WhenEveryIndexIsDegraded_RemainsInsideTier()
    {
        for (var index = 0; index <= byte.MaxValue; index++)
        {
            var source = ReferenceColors.Get(index);
            var basic = TerminalPalette.Project(source, ColorDepth.Basic16);
            var color256 = TerminalPalette.Project(source, ColorDepth.Indexed256);

            basic.IsRgb.ShouldBeTrue();
            TerminalPalette.FindPosition(basic, ColorDepth.Basic16).ShouldBeLessThan(16);
            color256.ShouldBe(source);
            TerminalPalette.FindPosition(color256, ColorDepth.Indexed256).ShouldBeInRange(0, 255);
        }
    }

    /// <summary>Verifies one palette position resolves to its exact RGB components.</summary>
    [Fact]
    public void ColorAt_WhenPositionIsSupplied_ReturnsReferenceRgb() =>
        TerminalPalette.ColorAt(67).ShouldBe(Color.Rgb(95, 135, 175));

    /// <summary>Verifies palette-position lookup validates its bounded input.</summary>
    [Fact]
    public void ColorAt_WhenPositionIsOutsidePalette_Throws()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(() => TerminalPalette.ColorAt(-1));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => TerminalPalette.ColorAt(256));
    }

    /// <summary>Verifies projection is deterministic and idempotent for random RGB colors.</summary>
    [Fact]
    public void Project_WhenRandomColorsAreRepeated_IsDeterministicAndIdempotent()
    {
        var random = new Random(0x00C01012);

        for (var iteration = 0; iteration < 2_000; iteration++)
        {
            var source = Color.Rgb(random.Next(256), random.Next(256), random.Next(256));

            foreach (var depth in Enum.GetValues<ColorDepth>())
            {
                var first = TerminalPalette.Project(source, depth);
                var second = TerminalPalette.Project(source, depth);

                second.ShouldBe(first);
                TerminalPalette.Project(first, depth).ShouldBe(first);
            }
        }
    }

    /// <summary>Verifies unresolved and transparent colors cannot cross the terminal palette boundary.</summary>
    [Fact]
    public void Project_WhenColorIsNotConcrete_Throws() =>
        _ = Should.Throw<ArgumentException>(() => TerminalPalette.Project(Color.Transparent, ColorDepth.TrueColor));
}
